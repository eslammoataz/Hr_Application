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

    /// <summary>
    /// Resolve an OrgNode workflow step into one or more planned approver steps using hierarchy validation, cached node names, and manager filtering.
    /// </summary>
    /// <param name="step">The workflow step DTO containing the target OrgNodeId and hierarchy bypass options.</param>
    /// <param name="context">Resolution context providing requester info, cached org nodes and managers, and hierarchy data.</param>
    /// <param name="state">Resolution state used to track and deduplicate already-added planned steps.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A Result containing a list of PlannedStepDto when successful. The list will be empty if no managers or approvers are applicable, the requester is a manager at their own node (and that condition applies), or the step was already added to the state. 
    /// Returns failure with DomainErrors.Request.MissingOrgNodeId when the step has no OrgNodeId, or DomainErrors.Request.InvalidWorkflowChain when hierarchy validation fails.
    /// </returns>
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

        var nodeName = context.OrgNodesById.TryGetValue(step.OrgNodeId.Value, out var cachedNode)
            ? cachedNode.Name
            : (await _getNodeById(step.OrgNodeId.Value, ct))?.Name ?? step.OrgNodeId.ToString()!;

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
