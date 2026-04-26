using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Infrastructure.Services.Workflow.Resolvers;

public sealed class HierarchyLevelStepResolver : WorkflowStepResolverBase
{
    private readonly ILogger _logger;
    private readonly LoggingOptions _loggingOptions;
    private readonly string _logAction;

    public HierarchyLevelStepResolver(
        ILogger logger,
        LoggingOptions loggingOptions,
        string logAction)
    {
        _logger = logger;
        _loggingOptions = loggingOptions;
        _logAction = logAction;
    }

    public override WorkflowStepType Type => WorkflowStepType.HierarchyLevel;

    public override Task<Result<List<PlannedStepDto>>> ResolveAsync(
        WorkflowStepDto step,
        WorkflowResolutionContext context,
        WorkflowResolutionState state,
        CancellationToken ct)
    {
        _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
            "HierarchyLevelResolver_Start", new { LevelsUp = step.LevelsUp, StartFromLevel = step.StartFromLevel, RequesterId = context.RequesterEmployeeId });

        if (!step.LevelsUp.HasValue || step.LevelsUp.Value < 1)
        {
            _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                "HierarchyLevelResolver_InvalidLevels", new { LevelsUp = step.LevelsUp });
            return Task.FromResult(Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingLevelsUp));
        }

        var startLevel = step.StartFromLevel ?? 1;
        var endLevel = startLevel + step.LevelsUp.Value - 1;
        var results = new List<PlannedStepDto>();

        for (int level = startLevel; level <= endLevel; level++)
        {
            if (level > context.LevelNodes.Count)
            {
                _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                    "HierarchyLevelResolver_ChainExhausted", new { RequestedLevel = level, AvailableLevels = context.LevelNodes.Count });
                break;
            }

            var node = context.LevelNodes[level - 1];
            if (!context.ManagersByNodeId.TryGetValue(node.Id, out var managers) || managers.Count == 0)
            {
                _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                    "HierarchyLevelResolver_NoManagers", new { Level = level, NodeName = node.Name });
                continue;
            }

            var approvers = FilterApprovers(managers, context.RequesterEmployeeId, state.SeenApproverIds);
            if (approvers.Count == 0)
            {
                _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                    "HierarchyLevelResolver_NoApprovers", new { Level = level, NodeName = node.Name });
                continue;
            }

            var plannedStep = CreateStep(WorkflowStepType.HierarchyLevel, node.Name, approvers, nodeId: node.Id, resolvedFromLevel: level);

            if (!state.TryAddStep(plannedStep))
            {
                _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                    "HierarchyLevelResolver_Duplicate", new { Level = level, NodeName = node.Name });
                continue;
            }

            results.Add(plannedStep);
        }

        _logger.LogBusinessFlow(_loggingOptions, _logAction, LogStage.Processing,
            "HierarchyLevelResolver_Complete", new { StepsCreated = results.Count, TotalLevels = endLevel - startLevel + 1 });

        return Task.FromResult(Result.Success(results));
    }
}
