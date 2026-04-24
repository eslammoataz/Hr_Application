using System.Diagnostics;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Services.Workflow;
using HrSystemApp.Infrastructure.Services.Workflow.Resolvers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Infrastructure.Services;

public class WorkflowResolutionService : IWorkflowResolutionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WorkflowResolutionService> _logger;
    private readonly LoggingOptions _loggingOptions;
    private readonly WorkflowStepResolverFactory _resolverFactory;
    private readonly string _logAction = LogAction.Workflow.CreateRequest;

    public WorkflowResolutionService(
        IUnitOfWork unitOfWork,
        ILogger<WorkflowResolutionService> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
        _resolverFactory = CreateResolverFactory();
    }

    private WorkflowStepResolverFactory CreateResolverFactory()
    {
        var resolvers = new IWorkflowStepResolver[]
        {
            new DirectEmployeeStepResolver(
                (id, ct) => _unitOfWork.Employees.GetByIdAsync(id, ct)),

            new HierarchyLevelStepResolver(
                _logger,
                _loggingOptions,
                _logAction),

            new CompanyRoleStepResolver(
                (id, ct) => _unitOfWork.CompanyRoles.GetByIdAsync(id, ct)),

            new OrgNodeStepResolver(
                (id, ct) => _unitOfWork.OrgNodes.GetByIdAsync(id, ct))
        };

        return new WorkflowStepResolverFactory(resolvers);
    }

    public async Task<Result<List<PlannedStepDto>>> BuildApprovalChainAsync(
        Guid requesterEmployeeId,
        Guid requesterNodeId,
        List<WorkflowStepDto> definitionSteps,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var context = await LoadContextAsync(requesterEmployeeId, requesterNodeId, definitionSteps, ct);
            if (context.IsFailure)
                return Result.Failure<List<PlannedStepDto>>(context.Error);

            var state = new WorkflowResolutionState();
            var sortedSteps = definitionSteps.OrderBy(s => s.SortOrder).ToList();

            foreach (var step in sortedSteps)
            {
                var resolverResult = _resolverFactory.Get(step.StepType);
                if (resolverResult.IsFailure)
                    return Result.Failure<List<PlannedStepDto>>(resolverResult.Error);

                var result = await resolverResult.Value.ResolveAsync(step, context.Value, state, ct);
                if (result.IsFailure)
                    return result;
            }

            sw.Stop();
            _logger.LogActionSuccess(_loggingOptions, _logAction, sw.ElapsedMilliseconds);

            if (state.PlannedSteps.Count == 0)
            {
                _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                    "EmptyChain", new { EmployeeId = requesterEmployeeId, NodeId = requesterNodeId });
            }

            return Result.Success(state.PlannedSteps);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var lastKnownState = new { EmployeeId = requesterEmployeeId, NodeId = requesterNodeId };
            _logger.LogActionFailure(_loggingOptions, _logAction, LogStage.Processing, ex, lastKnownState);
            throw;
        }
    }

    private async Task<Result<WorkflowResolutionContext>> LoadContextAsync(
        Guid requesterEmployeeId,
        Guid requesterNodeId,
        List<WorkflowStepDto> definitionSteps,
        CancellationToken ct)
    {
        _logger.LogDecision(_loggingOptions, _logAction, LogStage.ExternalCall,
            "GetAncestorsAsync", new { NodeId = requesterNodeId });

        var isManagerAtOwnNode = await _unitOfWork.OrgNodeAssignments
            .IsManagerAtNodeAsync(requesterEmployeeId, requesterNodeId, ct);

        var ownNode = await _unitOfWork.OrgNodes.GetByIdAsync(requesterNodeId, ct);
        if (ownNode == null)
        {
            _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                "NodeNotFound", new { NodeId = requesterNodeId });
            return Result.Failure<WorkflowResolutionContext>(DomainErrors.OrgNode.NotFound);
        }

        var ancestors = await _unitOfWork.OrgNodes.GetAncestorsAsync(requesterNodeId, ct);
        var ancestorIds = ancestors.Select(a => a.Id).ToHashSet();

        _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
            "AncestorsResolved", new { AncestorCount = ancestors.Count });

        var levelNodes = new List<OrgNode> { ownNode };
        levelNodes.AddRange(ancestors);

        var nodeIdsToFetch = CollectNodeIdsForBatchFetch(definitionSteps, levelNodes);
        var managersByNodeId = nodeIdsToFetch.Count > 0
            ? await _unitOfWork.OrgNodeAssignments.GetManagersByNodesAsync(nodeIdsToFetch, ct)
            : new Dictionary<Guid, IReadOnlyList<Employee>>();

        var orgNodesById = nodeIdsToFetch.Count > 0
            ? await _unitOfWork.OrgNodes.GetByIdsAsync(nodeIdsToFetch, ct)
            : new Dictionary<Guid, OrgNode>();

        var (employeesById, rolesById, roleHoldersByRoleId) = await BatchFetchRelatedDataAsync(
            definitionSteps, ct);

        var context = new WorkflowResolutionContext
        {
            RequesterEmployeeId = requesterEmployeeId,
            RequesterNodeId = requesterNodeId,
            IsManagerAtOwnNode = isManagerAtOwnNode,
            LevelNodes = levelNodes,
            AncestorIds = ancestorIds,
            ManagersByNodeId = managersByNodeId,
            OrgNodesById = orgNodesById,
            EmployeesById = employeesById,
            RolesById = rolesById,
            RoleHoldersByRoleId = roleHoldersByRoleId
        };

        return Result.Success(context);
    }

    private HashSet<Guid> CollectNodeIdsForBatchFetch(List<WorkflowStepDto> steps, List<OrgNode> levelNodes)
    {
        var nodeIdsToFetch = new HashSet<Guid>();

        foreach (var step in steps)
        {
            if (step.StepType == WorkflowStepType.HierarchyLevel)
            {
                if (!step.LevelsUp.HasValue || step.LevelsUp.Value < 1)
                    continue;

                var startLevel = step.StartFromLevel ?? 1;
                var endLevel = startLevel + step.LevelsUp.Value - 1;
                for (int level = startLevel; level <= endLevel && level <= levelNodes.Count; level++)
                    nodeIdsToFetch.Add(levelNodes[level - 1].Id);
            }
            else if (step.StepType == WorkflowStepType.OrgNode && step.OrgNodeId.HasValue)
            {
                nodeIdsToFetch.Add(step.OrgNodeId.Value);
            }
        }

        return nodeIdsToFetch;
    }

    private async Task<(Dictionary<Guid, Employee> Employees,
        Dictionary<Guid, CompanyRole> Roles,
        Dictionary<Guid, IReadOnlyList<Employee>> RoleHolders)> BatchFetchRelatedDataAsync(
        List<WorkflowStepDto> steps, CancellationToken ct)
    {
        var employeeIds = steps
            .Where(s => s.StepType == WorkflowStepType.DirectEmployee && s.DirectEmployeeId.HasValue)
            .Select(s => s.DirectEmployeeId!.Value)
            .Distinct()
            .ToList();

        var roleIds = steps
            .Where(s => s.StepType == WorkflowStepType.CompanyRole && s.CompanyRoleId.HasValue)
            .Select(s => s.CompanyRoleId!.Value)
            .Distinct()
            .ToList();

        var employeesTask = Task.WhenAll(employeeIds.Select(id => _unitOfWork.Employees.GetByIdAsync(id, ct)));
        var rolesTask = Task.WhenAll(roleIds.Select(id => _unitOfWork.CompanyRoles.GetByIdAsync(id, ct)));
        var roleHoldersTask = Task.WhenAll(roleIds.Select(id => _unitOfWork.EmployeeCompanyRoles.GetActiveEmployeesByRoleIdAsync(id, ct)));

        await Task.WhenAll(employeesTask, rolesTask, roleHoldersTask);

        var employeesById = (await employeesTask)
            .Where(e => e != null)
            .ToDictionary(e => e!.Id, e => e!);

        var rolesById = (await rolesTask)
            .Where(r => r != null)
            .ToDictionary(r => r!.Id, r => r!);

        var holderResults = await roleHoldersTask;
        var roleHoldersByRoleId = roleIds
            .Zip(holderResults, (roleId, holders) => (roleId, holders))
            .ToDictionary(x => x.roleId, x => (IReadOnlyList<Employee>)x.holders);

        return (employeesById, rolesById, roleHoldersByRoleId);
    }

    public async Task<Result> ValidateWorkflowStepsAsync(
        Guid requesterEmployeeId,
        Guid requesterNodeId,
        List<WorkflowStepDto> definitionSteps,
        CancellationToken ct)
    {
        var chainResult = await BuildApprovalChainAsync(
            requesterEmployeeId,
            requesterNodeId,
            definitionSteps,
            ct);

        return chainResult.IsSuccess
            ? Result.Success()
            : Result.Failure(chainResult.Error);
    }
}
