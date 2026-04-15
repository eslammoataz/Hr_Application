using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IRequestRepository : IRepository<Request>
{
    Task<Request?> GetByIdWithHistoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Request>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);

    [Obsolete("Role-based pending approvals is deprecated. Use OrgNode-based workflow.")]
    Task<List<Request>> GetPendingApprovalsAsync(Guid approverId, CancellationToken cancellationToken = default);
}
