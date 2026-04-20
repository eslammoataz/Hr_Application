using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IRequestRepository : IRepository<Request>
{
    Task<Request?> GetByIdWithHistoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Request>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<List<Request>> GetPendingApprovalsAsync(Guid approverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all approval-history entries where the given employee took an action (Approved or Rejected),
    /// together with the parent Request and the requester Employee.
    /// </summary>
    Task<List<RequestApprovalHistory>> GetApprovalActionsAsync(Guid approverId, CancellationToken cancellationToken = default);
}
