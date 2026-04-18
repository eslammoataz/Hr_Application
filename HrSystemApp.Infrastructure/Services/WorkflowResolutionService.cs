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

        // Check if employee is a manager at their own node
        var isManagerAtOwnNode = await _unitOfWork.OrgNodeAssignments
            .IsManagerAtNodeAsync(requesterEmployeeId, requesterNodeId, ct);

        // Get own node details
        var ownNode = await _unitOfWork.OrgNodes.GetByIdAsync(requesterNodeId, ct);
        if (ownNode == null)
        {
            _logger.LogWarning("Employee's node {NodeId} not found", requesterNodeId);
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.OrgNode.NotFound);
        }

        // Get all ancestors of the requester's node (ordered from immediate parent to root)
        var ancestors = await _unitOfWork.OrgNodes.GetAncestorsAsync(requesterNodeId, ct);
        var ancestorIds = ancestors.Select(a => a.Id).ToHashSet();

        // Sort definition steps by sort order
        var sortedSteps = definitionSteps.OrderBy(s => s.SortOrder).ToList();

        var plannedSteps = new List<PlannedStepDto>();

        foreach (var step in sortedSteps)
        {
            if (step.StepType == WorkflowStepType.DirectEmployee)
            {
                // ── DIRECT EMPLOYEE STEP ─────────────────────────────────────────────

                if (step.DirectEmployeeId == null)
                    return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingDirectEmployeeId);

                var directEmployee = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, ct);
                if (directEmployee == null)
                    return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.DirectEmployeeNotInCompany);

                // No hierarchy validation. No self-approval skip.
                // The admin explicitly named this person. They approve regardless.

                plannedSteps.Add(new PlannedStepDto
                {
                    StepType = WorkflowStepType.DirectEmployee,
                    NodeId = null,
                    NodeName = directEmployee.FullName,
                    SortOrder = step.SortOrder,
                    Approvers = new List<ApproverDto>
                    {
                        new ApproverDto
                        {
                            EmployeeId = directEmployee.Id,
                            EmployeeName = directEmployee.FullName
                        }
                    }
                });
            }
            else
            {
                // ── ORG NODE STEP ────────────────────────────────────────────────────

                if (step.OrgNodeId == null)
                    return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingOrgNodeId);

                // Self-approval skip: if this node is the requester's own node
                // and the requester is a manager there, skip this step.
                if (step.OrgNodeId == requesterNodeId && isManagerAtOwnNode)
                {
                    _logger.LogInformation("Skipping step {SortOrder}: employee {EmployeeId} is a manager at node {NodeId} (self-approval prevention)",
                        step.SortOrder, requesterEmployeeId, requesterNodeId);
                    continue;
                }

                // Hierarchy validation (skip if BypassHierarchyCheck is true)
                if (!step.BypassHierarchyCheck)
                {
                    if (step.OrgNodeId != requesterNodeId && !ancestorIds.Contains(step.OrgNodeId.Value))
                    {
                        _logger.LogWarning("Workflow step {StepOrder} references node {NodeId} which is not in the approval path",
                            step.SortOrder, step.OrgNodeId);
                        return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.InvalidWorkflowChain);
                    }
                }

                // Resolve the node object for its name
                OrgNode? stepNode;
                if (step.OrgNodeId == requesterNodeId)
                {
                    stepNode = ownNode;
                }
                else if (ancestorIds.Contains(step.OrgNodeId.Value))
                {
                    stepNode = ancestors.First(a => a.Id == step.OrgNodeId.Value);
                }
                else
                {
                    // BypassHierarchyCheck is true and it's not an ancestor — load it fresh
                    stepNode = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId.Value, ct);
                    if (stepNode == null)
                        return Result.Failure<List<PlannedStepDto>>(DomainErrors.OrgNode.NotFound);
                }

                // Get managers at this node
                var managers = await _unitOfWork.OrgNodeAssignments.GetManagersByNodeAsync(step.OrgNodeId.Value, ct);
                if (managers.Count == 0)
                {
                    _logger.LogWarning("No active managers at step {SortOrder} (node: {NodeName})",
                        step.SortOrder, stepNode.Name);
                    return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.NoActiveManagersAtStep);
                }

                // Exclude requester from approvers (self-approval prevention)
                var approvers = managers
                    .Where(m => m.Id != requesterEmployeeId)
                    .Select(m => new ApproverDto { EmployeeId = m.Id, EmployeeName = m.FullName })
                    .ToList();

                // Edge case: requester was the only manager — keep them
                if (approvers.Count == 0)
                {
                    approvers = managers
                        .Select(m => new ApproverDto { EmployeeId = m.Id, EmployeeName = m.FullName })
                        .ToList();
                    _logger.LogInformation("Self-approval edge case: requester is the only manager at step {SortOrder}", step.SortOrder);
                }

                plannedSteps.Add(new PlannedStepDto
                {
                    StepType = WorkflowStepType.OrgNode,
                    NodeId = stepNode.Id,
                    NodeName = stepNode.Name,
                    SortOrder = step.SortOrder,
                    Approvers = approvers
                });
            }
        }

        // If all steps were skipped (e.g., employee is manager at all definition nodes),
        // return empty list - caller will handle auto-approval
        if (plannedSteps.Count == 0)
        {
            _logger.LogInformation("Approval chain is empty for employee {EmployeeId}. All steps were skipped due to self-approval prevention. Request will be auto-approved.",
                requesterEmployeeId);
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
