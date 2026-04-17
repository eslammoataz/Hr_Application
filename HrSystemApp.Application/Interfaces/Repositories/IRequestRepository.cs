using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IRequestRepository : IRepository<Request>
{
    Task<Request?> GetByIdWithHistoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Request>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<List<Request>> GetPendingApprovalsAsync(Guid approverId, CancellationToken cancellationToken = default);
}
