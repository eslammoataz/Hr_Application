using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.Requests.Commands.Admin;

public record CreateRequestDefinitionCommand : IRequest<Result<Guid>>
{
    public Guid? CompanyId { get; set; }
    public RequestType RequestType { get; set; }
    public List<WorkflowStepDto> Steps { get; set; } = new();
}

public class CreateRequestDefinitionCommandHandler : IRequestHandler<CreateRequestDefinitionCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CreateRequestDefinitionCommandHandler> _logger;

    public CreateRequestDefinitionCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<CreateRequestDefinitionCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateRequestDefinitionCommand request, CancellationToken cancellationToken)
    {
        Guid targetCompanyId;

        if (_currentUserService.Role == nameof(UserRole.SuperAdmin))
        {
            if (!request.CompanyId.HasValue || request.CompanyId.Value == Guid.Empty)
                return Result.Failure<Guid>(DomainErrors.General.ArgumentError);

            targetCompanyId = request.CompanyId.Value;
        }
        else
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);

            var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
            if (employee == null)
                return Result.Failure<Guid>(DomainErrors.Employee.NotFound);

            targetCompanyId = employee.CompanyId;
        }

        _logger.LogInformation("Attempting to create Request Definition for Company {CompanyId} (Type: {Type}).",
            targetCompanyId, request.RequestType);

        // 1. Check if already exists
        var existing = await _unitOfWork.RequestDefinitions.GetByTypeAsync(targetCompanyId, request.RequestType, cancellationToken);
        if (existing != null)
        {
            _logger.LogWarning("CreateRequestDefinition failed: Definition already exists for Company {CompanyId}, Type {Type}.",
                targetCompanyId, request.RequestType);
            return Result.Failure<Guid>(DomainErrors.Requests.DefinitionAlreadyExists);
        }

        // 2. Validate steps have unique sort orders
        var sortOrders = request.Steps.Select(s => s.SortOrder).ToList();
        if (sortOrders.Distinct().Count() != sortOrders.Count)
        {
            _logger.LogWarning("CreateRequestDefinition failed: Duplicate sort orders detected.");
            return Result.Failure<Guid>(DomainErrors.General.ArgumentError);
        }

        // (NEW) Per-step field consistency
        foreach (var step in request.Steps)
        {
            if (step.StepType == WorkflowStepType.HierarchyLevel)
            {
                // HierarchyLevel must have LevelsUp >= 1
                if (!step.LevelsUp.HasValue || step.LevelsUp.Value < 1)
                    return Result.Failure<Guid>(DomainErrors.Request.MissingLevelsUp);

                // StartFromLevel (if set) must be >= 1
                if (step.StartFromLevel.HasValue && step.StartFromLevel.Value < 1)
                    return Result.Failure<Guid>(DomainErrors.Request.InvalidStartFromLevel);

                // HierarchyLevel must NOT have OrgNodeId, DirectEmployeeId, or BypassHierarchyCheck
                if (step.OrgNodeId.HasValue || step.DirectEmployeeId.HasValue || step.BypassHierarchyCheck)
                    return Result.Failure<Guid>(DomainErrors.Request.UnexpectedFieldsOnHierarchyLevelStep);
            }
            else
            {
                // OrgNode, DirectEmployee, and CompanyRole steps must NOT have StartFromLevel or LevelsUp
                if (step.StartFromLevel.HasValue || step.LevelsUp.HasValue)
                    return Result.Failure<Guid>(DomainErrors.Request.HierarchyLevelFieldsOnNonHierarchyStep);
            }
        }

        // (NEW) HierarchyLevel ranges must not overlap
        var hierarchyRanges = request.Steps
            .Where(s => s.StepType == WorkflowStepType.HierarchyLevel)
            .Select(s => new
            {
                Start = s.StartFromLevel ?? 1,
                End = (s.StartFromLevel ?? 1) + s.LevelsUp!.Value - 1,
                s.SortOrder
            })
            .ToList();

        for (int i = 0; i < hierarchyRanges.Count; i++)
        {
            for (int j = i + 1; j < hierarchyRanges.Count; j++)
            {
                var a = hierarchyRanges[i];
                var b = hierarchyRanges[j];
                // Overlap test: max(start) <= min(end)
                if (Math.Max(a.Start, b.Start) <= Math.Min(a.End, b.End))
                {
                    _logger.LogWarning("HierarchyLevel ranges overlap between steps sortOrder {A} [{As}..{Ae}] and {B} [{Bs}..{Be}]",
                        a.SortOrder, a.Start, a.End, b.SortOrder, b.Start, b.End);
                    return Result.Failure<Guid>(DomainErrors.Request.HierarchyRangesOverlap);
                }
            }
        }

        // 3. Validate each step's referenced entity exists and belongs to this company
        foreach (var step in request.Steps)
        {
            if (step.StepType == WorkflowStepType.OrgNode)
            {
                if (!step.OrgNodeId.HasValue)
                    return Result.Failure<Guid>(DomainErrors.Request.MissingOrgNodeId);

                var node = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId.Value, cancellationToken);
                if (node == null)
                    return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);

                if (node.CompanyId != targetCompanyId)
                    return Result.Failure<Guid>(DomainErrors.Request.OrgNodeNotInCompany);
            }
            else if (step.StepType == WorkflowStepType.DirectEmployee)
            {
                if (!step.DirectEmployeeId.HasValue)
                    return Result.Failure<Guid>(DomainErrors.Request.MissingDirectEmployeeId);

                var directEmp = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, cancellationToken);
                if (directEmp == null || directEmp.CompanyId != targetCompanyId)
                    return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeNotInCompany);

                if (directEmp.EmploymentStatus != EmploymentStatus.Active)
                    return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeNotActive);
            }
            else if (step.StepType == WorkflowStepType.CompanyRole)
            {
                if (!step.CompanyRoleId.HasValue)
                    return Result.Failure<Guid>(DomainErrors.Request.MissingCompanyRoleId);

                var role = await _unitOfWork.CompanyRoles.GetByIdAsync(step.CompanyRoleId.Value, cancellationToken);
                if (role is null || role.IsDeleted || role.CompanyId != targetCompanyId)
                    return Result.Failure<Guid>(DomainErrors.Request.RoleNotInCompany);
            }
        }

        // 4. Create definition
        var definition = new RequestDefinition
        {
            CompanyId = targetCompanyId,
            RequestType = request.RequestType,
            IsActive = true
        };

        foreach (var step in request.Steps)
        {
            definition.WorkflowSteps.Add(new RequestWorkflowStep
            {
                StepType = step.StepType,
                OrgNodeId = step.OrgNodeId,
                BypassHierarchyCheck = step.BypassHierarchyCheck,
                DirectEmployeeId = step.DirectEmployeeId,
                StartFromLevel = step.StartFromLevel,
                LevelsUp = step.LevelsUp,
                CompanyRoleId = step.CompanyRoleId,
                SortOrder = step.SortOrder
            });
        }

        await _unitOfWork.RequestDefinitions.AddAsync(definition, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Request definition created successfully — DefinitionId={DefinitionId}, Type={Type}",
            definition.Id, definition.RequestType);

        return Result.Success(definition.Id);
    }
}
