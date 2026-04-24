using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Infrastructure.Services.Workflow.Resolvers;

public sealed class CompanyRoleStepResolver : WorkflowStepResolverBase
{
    private readonly Func<Guid, CancellationToken, Task<CompanyRole?>> _getRoleById;

    public CompanyRoleStepResolver(Func<Guid, CancellationToken, Task<CompanyRole?>> getRoleById)
    {
        _getRoleById = getRoleById;
    }

    public override WorkflowStepType Type => WorkflowStepType.CompanyRole;

    public override async Task<Result<List<PlannedStepDto>>> ResolveAsync(
        WorkflowStepDto step,
        WorkflowResolutionContext context,
        WorkflowResolutionState state,
        CancellationToken ct)
    {
        if (!step.CompanyRoleId.HasValue)
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingCompanyRoleId);

        CompanyRole? role = null;
        if (!context.RolesById.TryGetValue(step.CompanyRoleId.Value, out role))
            role = await _getRoleById(step.CompanyRoleId.Value, ct);

        if (role == null)
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.RoleNotFound);

        if (!context.RoleHoldersByRoleId.TryGetValue(step.CompanyRoleId.Value, out var roleHolders))
            return Result.Success(new List<PlannedStepDto>());

        var approvers = FilterApprovers(roleHolders, context.RequesterEmployeeId, state.SeenApproverIds);
        if (approvers.Count == 0)
            return Result.Success(new List<PlannedStepDto>());

        var plannedStep = CreateStep(
            WorkflowStepType.CompanyRole,
            role.Name,
            approvers,
            companyRoleId: role.Id,
            roleName: role.Name);

        if (!state.TryAddStep(plannedStep))
            return Result.Success(new List<PlannedStepDto>());

        return Result.Success(new List<PlannedStepDto> { plannedStep });
    }
}
