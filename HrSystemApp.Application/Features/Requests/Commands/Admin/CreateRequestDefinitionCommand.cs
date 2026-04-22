using System.Diagnostics;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly LoggingOptions _loggingOptions;

    public CreateRequestDefinitionCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<CreateRequestDefinitionCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<Guid>> Handle(CreateRequestDefinitionCommand request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.Workflow.CreateRequestDefinition);

        Guid targetCompanyId;

        if (_currentUserService.Role == nameof(UserRole.SuperAdmin))
        {
            if (!request.CompanyId.HasValue || request.CompanyId.Value == Guid.Empty)
            {
                _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Validation,
                    "InvalidCompanyId", null);
                sw.Stop();
                return Result.Failure<Guid>(DomainErrors.General.ArgumentError);
            }
            targetCompanyId = request.CompanyId.Value;
        }
        else
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Authorization,
                    "UserNotAuthenticated", null);
                sw.Stop();
                return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);
            }

            var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
            if (employee == null)
            {
                _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Authorization,
                    "EmployeeNotFound", new { UserId = userId });
                sw.Stop();
                return Result.Failure<Guid>(DomainErrors.Employee.NotFound);
            }
            targetCompanyId = employee.CompanyId;
        }

        var existing = await _unitOfWork.RequestDefinitions.GetByTypeAsync(targetCompanyId, request.RequestType, cancellationToken);
        if (existing != null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Validation,
                "DefinitionAlreadyExists", new { CompanyId = targetCompanyId, RequestType = request.RequestType.ToString() });
            sw.Stop();
            return Result.Failure<Guid>(DomainErrors.Requests.DefinitionAlreadyExists);
        }

        var sortOrders = request.Steps.Select(s => s.SortOrder).ToList();
        if (sortOrders.Distinct().Count() != sortOrders.Count)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Validation,
                "DuplicateSortOrders", null);
            sw.Stop();
            return Result.Failure<Guid>(DomainErrors.General.ArgumentError);
        }

        foreach (var step in request.Steps)
        {
            if (step.StepType == WorkflowStepType.HierarchyLevel)
            {
                if (!step.LevelsUp.HasValue || step.LevelsUp.Value < 1)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Validation,
                        "MissingLevelsUp", new { SortOrder = step.SortOrder });
                    sw.Stop();
                    return Result.Failure<Guid>(DomainErrors.Request.MissingLevelsUp);
                }

                if (step.StartFromLevel.HasValue && step.StartFromLevel.Value < 1)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Validation,
                        "InvalidStartFromLevel", new { SortOrder = step.SortOrder });
                    sw.Stop();
                    return Result.Failure<Guid>(DomainErrors.Request.InvalidStartFromLevel);
                }

                if (step.OrgNodeId.HasValue || step.DirectEmployeeId.HasValue || step.BypassHierarchyCheck)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Validation,
                        "UnexpectedFieldsOnHierarchyLevel", new { SortOrder = step.SortOrder });
                    sw.Stop();
                    return Result.Failure<Guid>(DomainErrors.Request.UnexpectedFieldsOnHierarchyLevelStep);
                }
            }
            else
            {
                if (step.StartFromLevel.HasValue || step.LevelsUp.HasValue)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Validation,
                        "HierarchyFieldsOnNonHierarchyStep", new { SortOrder = step.SortOrder });
                    sw.Stop();
                    return Result.Failure<Guid>(DomainErrors.Request.HierarchyLevelFieldsOnNonHierarchyStep);
                }
            }
        }

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
                if (Math.Max(a.Start, b.Start) <= Math.Min(a.End, b.End))
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Validation,
                        "HierarchyRangesOverlap", new { A = a.SortOrder, B = b.SortOrder });
                    sw.Stop();
                    return Result.Failure<Guid>(DomainErrors.Request.HierarchyRangesOverlap);
                }
            }
        }

        foreach (var step in request.Steps)
        {
            if (step.StepType == WorkflowStepType.OrgNode)
            {
                if (!step.OrgNodeId.HasValue)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Validation,
                        "MissingOrgNodeId", new { SortOrder = step.SortOrder });
                    sw.Stop();
                    return Result.Failure<Guid>(DomainErrors.Request.MissingOrgNodeId);
                }

                var node = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId.Value, cancellationToken);
                if (node == null)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Validation,
                        "OrgNodeNotFound", new { OrgNodeId = step.OrgNodeId.Value });
                    sw.Stop();
                    return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
                }

                if (node.CompanyId != targetCompanyId)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Validation,
                        "OrgNodeNotInCompany", new { OrgNodeId = step.OrgNodeId.Value, CompanyId = targetCompanyId });
                    sw.Stop();
                    return Result.Failure<Guid>(DomainErrors.Request.OrgNodeNotInCompany);
                }
            }
            else if (step.StepType == WorkflowStepType.DirectEmployee)
            {
                if (!step.DirectEmployeeId.HasValue)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Validation,
                        "MissingDirectEmployeeId", new { SortOrder = step.SortOrder });
                    sw.Stop();
                    return Result.Failure<Guid>(DomainErrors.Request.MissingDirectEmployeeId);
                }

                var directEmp = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, cancellationToken);
                if (directEmp == null || directEmp.CompanyId != targetCompanyId)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Validation,
                        "DirectEmployeeNotInCompany", new { DirectEmployeeId = step.DirectEmployeeId.Value });
                    sw.Stop();
                    return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeNotInCompany);
                }

                if (directEmp.EmploymentStatus != EmploymentStatus.Active)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Validation,
                        "DirectEmployeeNotActive", new { DirectEmployeeId = step.DirectEmployeeId.Value });
                    sw.Stop();
                    return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeNotActive);
                }
            }
            else if (step.StepType == WorkflowStepType.CompanyRole)
            {
                if (!step.CompanyRoleId.HasValue)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Validation,
                        "MissingCompanyRoleId", new { SortOrder = step.SortOrder });
                    sw.Stop();
                    return Result.Failure<Guid>(DomainErrors.Request.MissingCompanyRoleId);
                }

                var role = await _unitOfWork.CompanyRoles.GetByIdAsync(step.CompanyRoleId.Value, cancellationToken);
                if (role is null || role.IsDeleted || role.CompanyId != targetCompanyId)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, LogStage.Validation,
                        "RoleNotInCompany", new { CompanyRoleId = step.CompanyRoleId.Value });
                    sw.Stop();
                    return Result.Failure<Guid>(DomainErrors.Request.RoleNotInCompany);
                }
            }
        }

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

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.Workflow.CreateRequestDefinition, sw.ElapsedMilliseconds);

        return Result.Success(definition.Id);
    }
}
