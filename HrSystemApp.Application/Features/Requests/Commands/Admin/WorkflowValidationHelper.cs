using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;

namespace HrSystemApp.Application.Features.Requests.Commands.Admin;

/// <summary>
/// Shared validation logic for request definition workflow steps.
/// NOTE: Role-based validation is deprecated. OrgNode-based steps are validated individually
/// in CreateRequestDefinitionCommandHandler and UpdateRequestDefinitionCommandHandler.
/// </summary>
[Obsolete("Role-based workflow validation is deprecated. Use OrgNode-based validation in command handlers.")]
internal static class WorkflowValidationHelper
{
    /// <summary>
    /// Validates workflow steps. This method is deprecated and always returns success.
    /// Validation is now done in command handlers using OrgNode validation.
    /// </summary>
    internal static Result<bool> ValidateWorkflowSteps(object steps, object hierarchyPositions)
    {
        // Deprecated: Validation is now handled in CreateRequestDefinitionCommandHandler
        // and UpdateRequestDefinitionCommandHandler using OrgNode-based validation.
        return Result.Success(true);
    }
}