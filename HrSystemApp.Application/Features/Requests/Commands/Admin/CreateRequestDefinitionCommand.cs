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

                var emp = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, cancellationToken);
                if (emp == null)
                    return Result.Failure<Guid>(DomainErrors.Employee.NotFound);

                if (emp.CompanyId != targetCompanyId)
                    return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeNotInCompany);
            }
        }

        // 4. Cross-step conflict check:
        //    A DirectEmployee approver must not also be a manager at any OrgNode step.
        var directEmployeeIds = request.Steps
            .Where(s => s.StepType == WorkflowStepType.DirectEmployee && s.DirectEmployeeId.HasValue)
            .Select(s => s.DirectEmployeeId!.Value)
            .ToHashSet();

        if (directEmployeeIds.Count > 0)
        {
            var orgNodeSteps = request.Steps.Where(s => s.StepType == WorkflowStepType.OrgNode && s.OrgNodeId.HasValue);
            foreach (var nodeStep in orgNodeSteps)
            {
                var managers = await _unitOfWork.OrgNodeAssignments.GetManagersByNodeAsync(nodeStep.OrgNodeId!.Value, cancellationToken);
                var conflict = managers.Any(m => directEmployeeIds.Contains(m.Id));
                if (conflict)
                    return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeAlsoNodeManager);
            }
        }

        // 5. Create Definition
        var definition = new RequestDefinition
        {
            CompanyId = targetCompanyId,
            RequestType = request.RequestType,
            IsActive = true,
            WorkflowSteps = request.Steps.Select(s => new RequestWorkflowStep
            {
                StepType = s.StepType,
                OrgNodeId = s.StepType == WorkflowStepType.OrgNode ? s.OrgNodeId : null,
                BypassHierarchyCheck = s.StepType == WorkflowStepType.OrgNode && s.BypassHierarchyCheck,
                DirectEmployeeId = s.StepType == WorkflowStepType.DirectEmployee ? s.DirectEmployeeId : null,
                SortOrder = s.SortOrder
            }).ToList()
        };

        await _unitOfWork.RequestDefinitions.AddAsync(definition, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully created Request Definition ID {DefinitionId} for Company {CompanyId} (Type: {Type}) with {StepCount} logic steps.",
            definition.Id, definition.CompanyId, definition.RequestType, definition.WorkflowSteps.Count);

        return Result.Success(definition.Id);
    }
}
