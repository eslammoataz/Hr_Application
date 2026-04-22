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

public record UpdateRequestDefinitionCommand : IRequest<Result<Guid>>
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }
    public List<WorkflowStepDto> Steps { get; set; } = new();
}

public class UpdateRequestDefinitionCommandHandler : IRequestHandler<UpdateRequestDefinitionCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UpdateRequestDefinitionCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public UpdateRequestDefinitionCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<UpdateRequestDefinitionCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<Guid>> Handle(UpdateRequestDefinitionCommand request, CancellationToken cancellationToken)
    {

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Authorization,
                "UserNotAuthenticated", null);
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);
        }

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Authorization,
                "EmployeeNotFound", new { UserId = userId });
            return Result.Failure<Guid>(DomainErrors.Employee.NotFound);
        }

        var definition = await _unitOfWork.RequestDefinitions.GetFirstOrDefaultAsync(
            d => d.Id == request.Id,
            cancellationToken,
            d => d.WorkflowSteps
        );

        if (definition == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Validation,
                "DefinitionNotFound", new { DefinitionId = request.Id });
            return Result.Failure<Guid>(DomainErrors.Requests.DefinitionNotFound);
        }

        if (definition.CompanyId != employee.CompanyId)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Authorization,
                "UnauthorizedCrossCompany", new { DefinitionId = request.Id, UserId = userId, EmployeeCompanyId = employee.CompanyId });
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);
        }

        var sortOrders = request.Steps.Select(s => s.SortOrder).ToList();
        if (sortOrders.Distinct().Count() != sortOrders.Count)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Validation,
                "DuplicateSortOrders", null);
            return Result.Failure<Guid>(DomainErrors.General.ArgumentError);
        }

        foreach (var step in request.Steps)
        {
            if (step.StepType == WorkflowStepType.HierarchyLevel)
            {
                if (!step.LevelsUp.HasValue || step.LevelsUp.Value < 1)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Validation,
                        "MissingLevelsUp", new { SortOrder = step.SortOrder });
                    return Result.Failure<Guid>(DomainErrors.Request.MissingLevelsUp);
                }

                if (step.StartFromLevel.HasValue && step.StartFromLevel.Value < 1)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Validation,
                        "InvalidStartFromLevel", new { SortOrder = step.SortOrder });
                    return Result.Failure<Guid>(DomainErrors.Request.InvalidStartFromLevel);
                }

                if (step.OrgNodeId.HasValue || step.DirectEmployeeId.HasValue || step.BypassHierarchyCheck)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Validation,
                        "UnexpectedFieldsOnHierarchyLevel", new { SortOrder = step.SortOrder });
                    return Result.Failure<Guid>(DomainErrors.Request.UnexpectedFieldsOnHierarchyLevelStep);
                }
            }
            else
            {
                if (step.StartFromLevel.HasValue || step.LevelsUp.HasValue)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Validation,
                        "HierarchyFieldsOnNonHierarchyStep", new { SortOrder = step.SortOrder });
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
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Validation,
                        "HierarchyRangesOverlap", new { A = a.SortOrder, B = b.SortOrder });
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
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Validation,
                        "MissingOrgNodeId", new { SortOrder = step.SortOrder });
                    return Result.Failure<Guid>(DomainErrors.Request.MissingOrgNodeId);
                }

                var node = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId.Value, cancellationToken);
                if (node == null)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Validation,
                        "OrgNodeNotFound", new { OrgNodeId = step.OrgNodeId.Value });
                    return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
                }

                if (node.CompanyId != definition.CompanyId)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Validation,
                        "OrgNodeNotInCompany", new { OrgNodeId = step.OrgNodeId.Value });
                    return Result.Failure<Guid>(DomainErrors.Request.OrgNodeNotInCompany);
                }
            }
            else if (step.StepType == WorkflowStepType.DirectEmployee)
            {
                if (!step.DirectEmployeeId.HasValue)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Validation,
                        "MissingDirectEmployeeId", new { SortOrder = step.SortOrder });
                    return Result.Failure<Guid>(DomainErrors.Request.MissingDirectEmployeeId);
                }

                var directEmp = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, cancellationToken);
                if (directEmp == null || directEmp.CompanyId != definition.CompanyId)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Validation,
                        "DirectEmployeeNotInCompany", new { DirectEmployeeId = step.DirectEmployeeId.Value });
                    return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeNotInCompany);
                }

                if (directEmp.EmploymentStatus != EmploymentStatus.Active)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Validation,
                        "DirectEmployeeNotActive", new { DirectEmployeeId = step.DirectEmployeeId.Value });
                    return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeNotActive);
                }
            }
            else if (step.StepType == WorkflowStepType.CompanyRole)
            {
                if (!step.CompanyRoleId.HasValue)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Validation,
                        "MissingCompanyRoleId", new { SortOrder = step.SortOrder });
                    return Result.Failure<Guid>(DomainErrors.Request.MissingCompanyRoleId);
                }

                var role = await _unitOfWork.CompanyRoles.GetByIdAsync(step.CompanyRoleId.Value, cancellationToken);
                if (role is null || role.IsDeleted || role.CompanyId != definition.CompanyId)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequestDefinition, LogStage.Validation,
                        "RoleNotInCompany", new { CompanyRoleId = step.CompanyRoleId.Value });
                    return Result.Failure<Guid>(DomainErrors.Request.RoleNotInCompany);
                }
            }
        }

        definition.WorkflowSteps.Clear();
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

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(definition.Id);
    }
}
