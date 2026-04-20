using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;

namespace HrSystemApp.Application.Interfaces.Services;

public interface IWorkflowResolutionService
{
    /// <summary>
    /// Builds the approval chain for a request.
    /// Validates all steps reference ancestors of the requester's node,
    /// then populates approvers (Managers) for each step.
    /// </summary>
    Task<Result<List<PlannedStepDto>>> BuildApprovalChainAsync(
        Guid requesterEmployeeId,
        Guid requesterNodeId,
        List<WorkflowStepDto> definitionSteps,
        CancellationToken ct);

    /// <summary>
    /// Validates that all workflow steps are valid (own node or ancestors) of the requester's node
    /// and that each step has at least one active manager.
    /// Skips validation for steps where the employee is a manager (self-approval prevention).
    /// </summary>
    Task<Result> ValidateWorkflowStepsAsync(
        Guid requesterEmployeeId,
        Guid requesterNodeId,
        List<WorkflowStepDto> definitionSteps,
        CancellationToken ct);
}
