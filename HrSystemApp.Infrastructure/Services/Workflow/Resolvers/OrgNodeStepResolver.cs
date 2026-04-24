using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Infrastructure.Services.Workflow.Resolvers;

public sealed class OrgNodeStepResolver : WorkflowStepResolverBase
{
    private readonly Func<Guid, CancellationToken, Task<OrgNode?>> _getNodeById;

    public OrgNodeStepResolver(Func<Guid, CancellationToken, Task<OrgNode?>> getNodeById)
    {
        _getNodeById = getNodeById;
    }

    public override WorkflowStepType Type => WorkflowStepType.OrgNode;

    public override async Task<Result<List<PlannedStepDto>>> ResolveAsync(
        WorkflowStepDto step,
        WorkflowResolutionContext context,
        WorkflowResolutionState state,
        CancellationToken ct)
    {
        if (step.OrgNodeId == null)
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingOrgNodeId);

        if (step.OrgNodeId == context.RequesterNodeId && context.IsManagerAtOwnNode)
            return Result.Success(new List<PlannedStepDto>());

        if (!step.BypassHierarchyCheck)
        {
            if (step.OrgNodeId != context.RequesterNodeId && !context.AncestorIds.Contains(step.OrgNodeId.Value))
                return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.InvalidWorkflowChain);
        }

        OrgNode? stepNode = await _getNodeById(step.OrgNodeId.Value, ct);
        var nodeName = stepNode?.Name ?? step.OrgNodeId.ToString()!;

        if (!context.ManagersByNodeId.TryGetValue(step.OrgNodeId.Value, out var managers) || managers.Count == 0)
            return Result.Success(new List<PlannedStepDto>());

        var approvers = FilterApprovers(managers, context.RequesterEmployeeId, state.SeenApproverIds);
        if (approvers.Count == 0)
            return Result.Success(new List<PlannedStepDto>());

        var plannedStep = CreateStep(
            WorkflowStepType.OrgNode,
            nodeName,
            approvers,
            nodeId: step.OrgNodeId);

        if (!state.TryAddStep(plannedStep))
            return Result.Success(new List<PlannedStepDto>());

        return Result.Success(new List<PlannedStepDto> { plannedStep });
    }
}
