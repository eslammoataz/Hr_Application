using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Infrastructure.Services.Workflow.Resolvers;

public sealed class OrgNodeStepResolver : WorkflowStepResolverBase
{
    private readonly Func<Guid, CancellationToken, Task<OrgNode?>> _getNodeById;
    private readonly ILogger _logger;
    private readonly LoggingOptions _loggingOptions;
    private readonly string _logAction;

    public OrgNodeStepResolver(
        Func<Guid, CancellationToken, Task<OrgNode?>> getNodeById,
        ILogger logger,
        LoggingOptions loggingOptions,
        string logAction = LogAction.Workflow.CreateRequest)
    {
        _getNodeById = getNodeById;
        _logger = logger;
        _loggingOptions = loggingOptions;
        _logAction = logAction;
    }

    public override WorkflowStepType Type => WorkflowStepType.OrgNode;

    public override async Task<Result<List<PlannedStepDto>>> ResolveAsync(
        WorkflowStepDto step,
        WorkflowResolutionContext context,
        WorkflowResolutionState state,
        CancellationToken ct)
    {
        _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
            "OrgNodeResolver_Start", new { OrgNodeId = step.OrgNodeId, RequesterNodeId = context.RequesterNodeId, BypassCheck = step.BypassHierarchyCheck });

        if (step.OrgNodeId == null)
        {
            _logger.LogWarning("[OrgNodeStepResolver] OrgNodeId is null");
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingOrgNodeId);
        }

        if (step.OrgNodeId == context.RequesterNodeId && context.IsManagerAtOwnNode)
        {
            _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                "OrgNodeResolver_SelfApproval", new { OrgNodeId = step.OrgNodeId });
            return Result.Success(new List<PlannedStepDto>());
        }

        if (!step.BypassHierarchyCheck)
        {
            if (step.OrgNodeId != context.RequesterNodeId && !context.AncestorIds.Contains(step.OrgNodeId.Value))
            {
                _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                    "OrgNodeResolver_NotInHierarchy", new { OrgNodeId = step.OrgNodeId, AncestorIds = context.AncestorIds });
                return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.InvalidWorkflowChain);
            }
        }

        var nodeName = context.OrgNodesById.TryGetValue(step.OrgNodeId.Value, out var cachedNode)
            ? cachedNode.Name
            : (await _getNodeById(step.OrgNodeId.Value, ct))?.Name ?? step.OrgNodeId.ToString()!;

        if (!context.ManagersByNodeId.TryGetValue(step.OrgNodeId.Value, out var managers) || managers.Count == 0)
        {
            _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                "OrgNodeResolver_NoManagers", new { OrgNodeId = step.OrgNodeId, NodeName = nodeName });
            return Result.Success(new List<PlannedStepDto>());
        }

        var approvers = FilterApprovers(managers, context.RequesterEmployeeId, state.SeenApproverIds);

        if (approvers.Count == 0)
        {
            _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                "OrgNodeResolver_NoApprovers", new { OrgNodeId = step.OrgNodeId, NodeName = nodeName });
            return Result.Success(new List<PlannedStepDto>());
        }

        var plannedStep = CreateStep(WorkflowStepType.OrgNode, nodeName, approvers, nodeId: step.OrgNodeId);

        if (!state.TryAddStep(plannedStep))
        {
            _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                "OrgNodeResolver_DuplicateStep", new { OrgNodeId = step.OrgNodeId });
            return Result.Success(new List<PlannedStepDto>());
        }

        _logger.LogBusinessFlow(_loggingOptions, _logAction, LogStage.Processing,
            "OrgNodeResolver_Success", new { OrgNodeId = step.OrgNodeId, NodeName = nodeName, ApproverCount = approvers.Count });

        return Result.Success(new List<PlannedStepDto> { plannedStep });
    }
}
