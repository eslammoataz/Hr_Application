using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Infrastructure.Services.Workflow.Resolvers;

public sealed class CompanyRoleStepResolver : WorkflowStepResolverBase
{
    private readonly Func<Guid, CancellationToken, Task<CompanyRole?>> _getRoleById;
    private readonly ILogger _logger;
    private readonly LoggingOptions _loggingOptions;
    private readonly string _logAction;

    public CompanyRoleStepResolver(
        Func<Guid, CancellationToken, Task<CompanyRole?>> getRoleById,
        ILogger logger,
        LoggingOptions loggingOptions,
        string logAction = LogAction.Workflow.CreateRequest)
    {
        _getRoleById = getRoleById;
        _logger = logger;
        _loggingOptions = loggingOptions;
        _logAction = logAction;
    }

    public override WorkflowStepType Type => WorkflowStepType.CompanyRole;

    public override async Task<Result<List<PlannedStepDto>>> ResolveAsync(
        WorkflowStepDto step,
        WorkflowResolutionContext context,
        WorkflowResolutionState state,
        CancellationToken ct)
    {
        _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
            "CompanyRoleResolver_Start", new { CompanyRoleId = step.CompanyRoleId, RequesterId = context.RequesterEmployeeId });

        if (!step.CompanyRoleId.HasValue)
        {
            _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                "CompanyRoleResolver_MissingRoleId", new { StepType = "CompanyRole" });
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingCompanyRoleId);
        }

        CompanyRole? role = null;
        if (!context.RolesById.TryGetValue(step.CompanyRoleId.Value, out role))
        {
            role = await _getRoleById(step.CompanyRoleId.Value, ct);
        }

        if (role == null)
        {
            _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                "CompanyRoleResolver_RoleNotFound", new { CompanyRoleId = step.CompanyRoleId });
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.RoleNotFound);
        }

        _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
            "CompanyRoleResolver_RoleFound", new { CompanyRoleId = step.CompanyRoleId, RoleName = role.Name, IsDeleted = role.IsDeleted, CompanyId = role.CompanyId });

        if (!context.RoleHoldersByRoleId.TryGetValue(step.CompanyRoleId.Value, out var roleHolders))
        {
            _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                "CompanyRoleResolver_NoRoleHolders", new { CompanyRoleId = step.CompanyRoleId });
            return Result.Success(new List<PlannedStepDto>());
        }

        _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
            "CompanyRoleResolver_RoleHoldersFound", new { CompanyRoleId = step.CompanyRoleId, HolderCount = roleHolders.Count });

        var approvers = FilterApprovers(roleHolders, context.RequesterEmployeeId, state.SeenApproverIds);

        if (approvers.Count == 0)
        {
            _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                "CompanyRoleResolver_NoApprovers", new { CompanyRoleId = step.CompanyRoleId, RequesterId = context.RequesterEmployeeId });
            return Result.Success(new List<PlannedStepDto>());
        }

        var plannedStep = CreateStep(WorkflowStepType.CompanyRole, role.Name, approvers, companyRoleId: role.Id, roleName: role.Name);

        if (!state.TryAddStep(plannedStep))
        {
            _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                "CompanyRoleResolver_DuplicateStep", new { CompanyRoleId = step.CompanyRoleId });
            return Result.Success(new List<PlannedStepDto>());
        }

        _logger.LogBusinessFlow(_loggingOptions, _logAction, LogStage.Processing,
            "CompanyRoleResolver_Success", new { CompanyRoleId = step.CompanyRoleId, RoleName = role.Name, ApproverCount = approvers.Count });

        return Result.Success(new List<PlannedStepDto> { plannedStep });
    }
}
