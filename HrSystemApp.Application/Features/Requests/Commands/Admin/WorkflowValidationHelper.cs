using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Features.Requests.Commands.Admin;

/// <summary>
/// Shared validation logic for request definition workflow steps against a company hierarchy.
/// </summary>
internal static class WorkflowValidationHelper
{
    /// <summary>
    /// Validates that all workflow steps use roles that exist in the hierarchy,
    /// and that the steps escalate authority in the correct order.
    /// </summary>
    internal static Result<bool> ValidateWorkflowSteps(
        List<WorkflowStepDto> steps,
        IReadOnlyList<CompanyHierarchyPosition> hierarchyPositions)
    {
        var hierarchySortMap = hierarchyPositions.ToDictionary(p => p.Role, p => p.SortOrder);

        // 1. All roles must exist in the hierarchy
        var missingRoles = steps
            .Select(s => s.Role)
            .Distinct()
            .Where(r => !hierarchySortMap.ContainsKey(r))
            .ToList();

        if (missingRoles.Any())
            return Result.Failure<bool>(new Error("Hierarchy.WorkflowRoleNotInHierarchy",
                $"The following roles must be added to your company hierarchy before they can be used in a workflow: [{string.Join(", ", missingRoles)}]"));

        // 2. Steps must escalate authority (each step's role must have lower SortOrder = higher authority)
        var orderedSteps = steps.OrderBy(s => s.SortOrder).ToList();
        for (var i = 1; i < orderedSteps.Count; i++)
        {
            var prevHierarchyOrder = hierarchySortMap[orderedSteps[i - 1].Role];
            var currHierarchyOrder = hierarchySortMap[orderedSteps[i].Role];

            if (currHierarchyOrder >= prevHierarchyOrder)
                return Result.Failure<bool>(new Error("Hierarchy.InvalidStepOrder",
                    $"Step {i + 1} role '{orderedSteps[i].Role}' must have higher authority than step {i} role '{orderedSteps[i - 1].Role}'. Approval must escalate up the hierarchy."));
        }

        return Result.Success(true);
    }
}
