using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Services;

[Obsolete("Role-based workflow is deprecated. Use IWorkflowResolutionService for OrgNode-based workflow.")]
public interface IWorkflowService
{
    /// <summary>
    /// Calculates the full approval path for a request based on the current organization hierarchy
    /// and the admin-defined workflow template for the given request type.
    /// Skips vacant positions and prevents self-approval.
    /// </summary>
    [Obsolete("Use IWorkflowResolutionService.BuildApprovalChainAsync instead.")]
    Task<List<Employee>> GetApprovalPathAsync(Guid employeeId, RequestType requestType, CancellationToken cancellationToken = default);
}
