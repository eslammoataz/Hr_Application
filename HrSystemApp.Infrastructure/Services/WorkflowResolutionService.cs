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
            else if (step.StepType == WorkflowStepType.CompanyRole)
            {
                // ── COMPANY ROLE STEP ─────────────────────────────────────────────
                if (!step.CompanyRoleId.HasValue)
                    return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingCompanyRoleId);

                var role = await _unitOfWork.CompanyRoles.GetByIdAsync(step.CompanyRoleId.Value, ct);
                if (role is null)
                    return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.RoleNotFound);

                var roleHolders = await _unitOfWork.EmployeeCompanyRoles
                    .GetActiveEmployeesByRoleIdAsync(step.CompanyRoleId.Value, ct);

                var approvers = roleHolders
                    .Where(e => e.Id != requesterEmployeeId && !seenApproverIds.Contains(e.Id))
                    .Select(e => new ApproverDto { EmployeeId = e.Id, EmployeeName = e.FullName })
                    .ToList();

                if (approvers.Count == 0)
                {
                    _logger.LogInformation(
                        "CompanyRole step {SortOrder} (role: {RoleName}) skipped: no eligible role holders",
                        step.SortOrder, role.Name);
                    continue;
                }

                foreach (var a in approvers) seenApproverIds.Add(a.EmployeeId);

                plannedSteps.Add(new PlannedStepDto
                {
                    StepType = WorkflowStepType.CompanyRole,
                    NodeId = null,
                    NodeName = role.Name,
                    CompanyRoleId = role.Id,
                    RoleName = role.Name,
                    SortOrder = 0,
                    Approvers = approvers
                });
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
                if (step.OrgNodeId.HasValue)
                    stepNode = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId.Value, ct);
                else
                    stepNode = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId!.Value, ct);

                var nodeName = stepNode?.Name ?? step.OrgNodeId.ToString()!;

                // Collect managers at this node.
                var managers = await _unitOfWork.OrgNodeAssignments
                    .GetManagersByNodeAsync(step.OrgNodeId!.Value, ct);

                var approvers = managers
                    .Where(e => e.Id != requesterEmployeeId && !seenApproverIds.Contains(e.Id))
                    .Select(e => new ApproverDto { EmployeeId = e.Id, EmployeeName = e.FullName })
                    .ToList();

                if (approvers.Count == 0)
                {
                    _logger.LogInformation(
                        "OrgNode step {SortOrder} (node: {NodeName}) skipped: no eligible managers",
                        step.SortOrder, nodeName);
                    continue;
                }

                foreach (var a in approvers) seenApproverIds.Add(a.EmployeeId);

                plannedSteps.Add(new PlannedStepDto
                {
                    StepType = WorkflowStepType.OrgNode,
                    NodeId = step.OrgNodeId,
                    NodeName = nodeName,
                    SortOrder = step.SortOrder,
                    Approvers = approvers
                });
            }
        }

        return Result.Success(plannedSteps);
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
