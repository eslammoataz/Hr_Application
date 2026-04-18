using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Infrastructure.Services;

public class WorkflowResolutionService : IWorkflowResolutionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WorkflowResolutionService> _logger;

    public WorkflowResolutionService(IUnitOfWork unitOfWork, ILogger<WorkflowResolutionService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<List<PlannedStepDto>>> BuildApprovalChainAsync(
        Guid requesterEmployeeId,
        Guid requesterNodeId,
        List<WorkflowStepDto> definitionSteps,
        CancellationToken ct)
    {
        _logger.LogInformation("Building approval chain for employee {EmployeeId} from node {NodeId}",
            requesterEmployeeId, requesterNodeId);

        // Is the requester a manager at their own node? Used for self-approval skip below.
        var isManagerAtOwnNode = await _unitOfWork.OrgNodeAssignments
            .IsManagerAtNodeAsync(requesterEmployeeId, requesterNodeId, ct);

        // Load the requester's own node.
        var ownNode = await _unitOfWork.OrgNodes.GetByIdAsync(requesterNodeId, ct);
        if (ownNode == null)
        {
            _logger.LogWarning("Employee's node {NodeId} not found", requesterNodeId);
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.OrgNode.NotFound);
        }

        // Ancestors: immediate parent first, then grandparent, ..., then root.
        var ancestors = await _unitOfWork.OrgNodes.GetAncestorsAsync(requesterNodeId, ct);
        var ancestorIds = ancestors.Select(a => a.Id).ToHashSet();

        // Level-indexed chain: index 0 (Level 1) = own node, index 1 (Level 2) = immediate parent, etc.
        var levelNodes = new List<OrgNode> { ownNode };
        levelNodes.AddRange(ancestors);

        // Sort definition steps by their declared SortOrder.
        var sortedSteps = definitionSteps.OrderBy(s => s.SortOrder).ToList();

        var plannedSteps = new List<PlannedStepDto>();
        // Dedup across the entire resolved chain: an employee appears only at their earliest level.
        var seenApproverIds = new HashSet<Guid>();

        foreach (var step in sortedSteps)
        {
            if (step.StepType == WorkflowStepType.DirectEmployee)
            {
                // ── DIRECT EMPLOYEE STEP ─────────────────────────────────────────
                if (step.DirectEmployeeId == null)
                    return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingDirectEmployeeId);

                var directEmployee = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, ct);
                if (directEmployee == null)
                    return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.DirectEmployeeNotInCompany);

                // Dedup: if this employee was already added at an earlier step, skip.
                if (seenApproverIds.Contains(directEmployee.Id))
                {
                    _logger.LogInformation("Dedup: DirectEmployee {EmployeeId} already present earlier, skipping", directEmployee.Id);
                    continue;
                }

                seenApproverIds.Add(directEmployee.Id);

                plannedSteps.Add(new PlannedStepDto
                {
                    StepType = WorkflowStepType.DirectEmployee,
                    NodeId = null,
                    NodeName = directEmployee.FullName,
                    SortOrder = 0, // renumbered at the end
                    Approvers = new List<ApproverDto>
                    {
                        new ApproverDto { EmployeeId = directEmployee.Id, EmployeeName = directEmployee.FullName }
                    }
                });
            }
            else if (step.StepType == WorkflowStepType.HierarchyLevel)
            {
                // ── HIERARCHY LEVEL STEP (dynamic) ───────────────────────────────
                if (!step.LevelsUp.HasValue || step.LevelsUp.Value < 1)
                    return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingLevelsUp);

                var startLevel = step.StartFromLevel ?? 1;
                var endLevel = startLevel + step.LevelsUp.Value - 1;

                for (int level = startLevel; level <= endLevel; level++)
                {
                    // Graceful truncation: requester has fewer ancestors than requested.
                    if (level > levelNodes.Count)
                    {
                        _logger.LogInformation("Requester chain exhausted at level {Level}; stopping HierarchyLevel expansion early", level);
                        break;
                    }

                    var node = levelNodes[level - 1]; // 1-based -> 0-based
                    var managers = await _unitOfWork.OrgNodeAssignments.GetManagersByNodeAsync(node.Id, ct);

                    // D4: skip levels with no managers.
                    if (managers.Count == 0)
                    {
                        _logger.LogInformation("Level {Level} (node {NodeName}) has no managers, skipping", level, node.Name);
                        continue;
                    }

                    // D5: self-approval skip. Exclude the requester from approvers.
                    var approvers = managers
                        .Where(m => m.Id != requesterEmployeeId)
                        .Select(m => new ApproverDto { EmployeeId = m.Id, EmployeeName = m.FullName })
                        .ToList();

                    // Edge: requester was the only manager — skip this level entirely.
                    if (approvers.Count == 0)
                    {
                        _logger.LogInformation("Level {Level} skipped: requester was the only manager", level);
                        continue;
                    }

                    // D6: dedup against the chain seen so far.
                    approvers = approvers.Where(a => !seenApproverIds.Contains(a.EmployeeId)).ToList();
                    if (approvers.Count == 0)
                    {
                        _logger.LogInformation("Level {Level} skipped: all managers already present earlier in the chain", level);
                        continue;
                    }

                    foreach (var a in approvers) seenApproverIds.Add(a.EmployeeId);

                    plannedSteps.Add(new PlannedStepDto
                    {
                        StepType = WorkflowStepType.HierarchyLevel,
                        NodeId = node.Id,
                        NodeName = node.Name,
                        SortOrder = 0, // renumbered at the end
                        Approvers = approvers,
                        ResolvedFromLevel = level
                    });
                }
            }
            else
            {
                // ── ORG NODE STEP ────────────────────────────────────────────────
                if (step.OrgNodeId == null)
                    return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingOrgNodeId);

                // Self-approval skip (existing behavior preserved).
                if (step.OrgNodeId == requesterNodeId && isManagerAtOwnNode)
                {
                    _logger.LogInformation("Skipping OrgNode step {SortOrder}: self-approval prevention", step.SortOrder);
                    continue;
                }

                // Hierarchy validation (existing behavior preserved).
                if (!step.BypassHierarchyCheck)
                {
                    if (step.OrgNodeId != requesterNodeId && !ancestorIds.Contains(step.OrgNodeId.Value))
                    {
                        _logger.LogWarning("OrgNode step {SortOrder} references node {NodeId} not in approval path",
                            step.SortOrder, step.OrgNodeId);
                        return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.InvalidWorkflowChain);
                    }
                }

                // Resolve node for its name.
                OrgNode? stepNode;
                if (step.OrgNodeId == requesterNodeId) stepNode = ownNode;
                else if (ancestorIds.Contains(step.OrgNodeId.Value))
                    stepNode = ancestors.First(a => a.Id == step.OrgNodeId.Value);
                else
                {
                    stepNode = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId.Value, ct);
                    if (stepNode == null)
                        return Result.Failure<List<PlannedStepDto>>(DomainErrors.OrgNode.NotFound);
                }

                var managers = await _unitOfWork.OrgNodeAssignments.GetManagersByNodeAsync(step.OrgNodeId.Value, ct);
                if (managers.Count == 0)
                {
                    _logger.LogWarning("No active managers at OrgNode step {SortOrder} (node: {NodeName})",
                        step.SortOrder, stepNode.Name);
                    return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.NoActiveManagersAtStep);
                }

                var approvers = managers
                    .Where(m => m.Id != requesterEmployeeId)
                    .Select(m => new ApproverDto { EmployeeId = m.Id, EmployeeName = m.FullName })
                    .ToList();

                // Edge: requester was the only manager — fall back to including them (existing behavior).
                if (approvers.Count == 0)
                {
                    approvers = managers
                        .Select(m => new ApproverDto { EmployeeId = m.Id, EmployeeName = m.FullName })
                        .ToList();
                }

                // D6: dedup against chain so far.
                approvers = approvers.Where(a => !seenApproverIds.Contains(a.EmployeeId)).ToList();
                if (approvers.Count == 0)
                {
                    _logger.LogInformation("OrgNode step {SortOrder} skipped entirely due to dedup", step.SortOrder);
                    continue;
                }

                foreach (var a in approvers) seenApproverIds.Add(a.EmployeeId);

                plannedSteps.Add(new PlannedStepDto
                {
                    StepType = WorkflowStepType.OrgNode,
                    NodeId = stepNode.Id,
                    NodeName = stepNode.Name,
                    SortOrder = 0, // renumbered at the end
                    Approvers = approvers
                });
            }
        }

        // Renumber SortOrder to 1..N contiguous on the resolved chain.
        for (int i = 0; i < plannedSteps.Count; i++)
            plannedSteps[i].SortOrder = i + 1;

        if (plannedSteps.Count == 0)
        {
            _logger.LogInformation("Approval chain empty for employee {EmployeeId}; request will auto-approve", requesterEmployeeId);
            return Result.Success(plannedSteps);
        }

        _logger.LogInformation("Built approval chain with {StepCount} steps", plannedSteps.Count);
        return Result.Success(plannedSteps);
    }

    public async Task<Result> ValidateWorkflowStepsAsync(
        Guid requesterEmployeeId,
        Guid requesterNodeId,
        List<WorkflowStepDto> definitionSteps,
        CancellationToken ct)
    {
        _logger.LogInformation("Validating {StepCount} workflow steps for node {NodeId}",
            definitionSteps.Count, requesterNodeId);

        // Check if employee is a manager at their own node
        var isManagerAtOwnNode = await _unitOfWork.OrgNodeAssignments
            .IsManagerAtNodeAsync(requesterEmployeeId, requesterNodeId, ct);

        // Get own node
        var ownNode = await _unitOfWork.OrgNodes.GetByIdAsync(requesterNodeId, ct);
        if (ownNode == null)
        {
            _logger.LogWarning("Employee's node {NodeId} not found", requesterNodeId);
            return Result.Failure(DomainErrors.OrgNode.NotFound);
        }

        // Get all ancestors
        var ancestors = await _unitOfWork.OrgNodes.GetAncestorsAsync(requesterNodeId, ct);
        var ancestorIds = ancestors.Select(a => a.Id).ToHashSet();

        // Check each step references a valid node (own node or ancestor)
        foreach (var step in definitionSteps)
        {
            if (step.StepType == WorkflowStepType.DirectEmployee)
            {
                if (step.DirectEmployeeId == null)
                    return Result.Failure(DomainErrors.Request.MissingDirectEmployeeId);

                var emp = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, ct);
                if (emp == null)
                    return Result.Failure(DomainErrors.Request.DirectEmployeeNotInCompany);

                // No hierarchy validation needed for direct employee steps
                continue;
            }

            // OrgNode step
            if (step.OrgNodeId == null)
                return Result.Failure(DomainErrors.Request.MissingOrgNodeId);

            if (step.OrgNodeId == requesterNodeId && isManagerAtOwnNode)
                continue; // will be skipped at build time

            if (!step.BypassHierarchyCheck)
            {
                if (step.OrgNodeId != requesterNodeId && !ancestorIds.Contains(step.OrgNodeId.Value))
                    return Result.Failure(DomainErrors.Request.InvalidWorkflowChain);
            }

            var managers = await _unitOfWork.OrgNodeAssignments.GetManagersByNodeAsync(step.OrgNodeId.Value, ct);
            if (managers.Count == 0)
                return Result.Failure(DomainErrors.Request.NoActiveManagersAtStep);
        }

        return Result.Success();
    }
}
