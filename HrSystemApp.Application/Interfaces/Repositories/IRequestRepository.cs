using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IRequestRepository : IRepository<Request>
{
    Task<Request?> GetByIdWithHistoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Request>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<List<Request>> GetPendingApprovalsAsync(Guid approverId, CancellationToken cancellationToken = default);
    Task<List<RequestApprovalHistory>> GetApprovalActionsAsync(Guid approverId, CancellationToken cancellationToken = default);

    IQueryable<Request> QueryByEmployeeId(Guid employeeId);
    IQueryable<Request> QueryByCompanyId(Guid companyId);
    IQueryable<Request> QueryPendingApprovals(Guid approverId);
    IQueryable<RequestApprovalHistory> QueryApprovalActions(Guid approverId);

    Task<int> CountAsync(IQueryable<Request> query, CancellationToken cancellationToken = default);
    Task<List<Request>> ToListAsync(IQueryable<Request> query, CancellationToken cancellationToken = default);
    Task<int> CountHistoryAsync(IQueryable<RequestApprovalHistory> query, CancellationToken cancellationToken = default);
    Task<List<RequestApprovalHistory>> ToListHistoryAsync(IQueryable<RequestApprovalHistory> query, CancellationToken cancellationToken = default);
}
