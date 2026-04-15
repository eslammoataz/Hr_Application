using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
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

        // Determine which nodes need approval:
        // 1. If employee is NOT a manager at their own node → own node's manager is first approver
        // 2. If employee IS a manager at their own node → skip own node, start from ancestors
        // 3. Then continue through ancestors per definition steps

        foreach (var step in sortedSteps)
        {
            // Determine which node this step references
            // If this step references the employee's own node:
            //   - If employee is manager → SKIP this step (no self-approval)
            //   - If employee is NOT manager → process normally (get managers at own node)
            // If this step references an ancestor node → process normally

            if (step.OrgNodeId == requesterNodeId && isManagerAtOwnNode)
            {
                // Employee is a manager at their own node → skip self-approval
                _logger.LogInformation("Skipping step {SortOrder}: employee {EmployeeId} is a manager at node {NodeId} (self-approval prevention)",
                    step.SortOrder, requesterEmployeeId, requesterNodeId);
                continue;
            }

            // Validate step references either the employee's own node OR an ancestor
            if (step.OrgNodeId != requesterNodeId && !ancestorIds.Contains(step.OrgNodeId))
            {
                _logger.LogWarning("Workflow step {StepOrder} references node {NodeId} which is not in the approval path (own node or ancestor)",
                    step.SortOrder, step.OrgNodeId);
                return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.InvalidWorkflowChain);
            }

            // Get the node details for this step
            var stepNode = step.OrgNodeId == requesterNodeId
                ? ownNode
                : ancestors.First(a => a.Id == step.OrgNodeId);

            // Get managers at this node
            var managers = await _unitOfWork.OrgNodeAssignments.GetManagersByNodeAsync(step.OrgNodeId, ct);

            if (managers.Count == 0)
            {
                _logger.LogWarning("No active managers at step {StepOrder} (node: {NodeName})",
                    step.SortOrder, stepNode.Name);
                return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.NoActiveManagersAtStep);
            }

            // Exclude requester from approvers (self-approval prevention)
            var approvers = managers
                .Where(m => m.Id != requesterEmployeeId)
                .Select(m => new ApproverDto
                {
                    EmployeeId = m.Id,
                    EmployeeName = m.FullName
                })
                .ToList();

            // If all managers were excluded (self-approval edge case), keep them
            if (approvers.Count == 0)
            {
                approvers = managers
                    .Select(m => new ApproverDto
                    {
                        EmployeeId = m.Id,
                        EmployeeName = m.FullName
                    })
                    .ToList();

                _logger.LogInformation("Self-approval edge case: requester {EmployeeId} was the only manager, keeping them",
                    requesterEmployeeId);
            }

            plannedSteps.Add(new PlannedStepDto
            {
                NodeId = stepNode.Id,
                NodeName = stepNode.Name,
                SortOrder = step.SortOrder,
                Approvers = approvers
            });
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
            if (step.OrgNodeId == requesterNodeId && isManagerAtOwnNode)
            {
                // Employee is a manager at own node → this step will be skipped
                continue;
            }

            if (step.OrgNodeId != requesterNodeId && !ancestorIds.Contains(step.OrgNodeId))
            {
                _logger.LogWarning("Step {SortOrder} references {NodeId} which is not in the approval path",
                    step.SortOrder, step.OrgNodeId);
                return Result.Failure(DomainErrors.Request.InvalidWorkflowChain);
            }

            // Check step has at least one manager
            var managers = await _unitOfWork.OrgNodeAssignments.GetManagersByNodeAsync(step.OrgNodeId, ct);
            if (managers.Count == 0)
            {
                var nodeName = step.OrgNodeId == requesterNodeId
                    ? ownNode.Name
                    : ancestors.FirstOrDefault(a => a.Id == step.OrgNodeId)?.Name ?? step.OrgNodeId.ToString();
                _logger.LogWarning("Step {SortOrder} (node: {NodeName}) has no active managers",
                    step.SortOrder, nodeName);
                return Result.Failure(DomainErrors.Request.NoActiveManagersAtStep);
            }
        }

        return Result.Success();
    }
}