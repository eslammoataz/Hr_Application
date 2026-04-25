using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class RequestRepository : Repository<Request>, IRequestRepository
{
    public RequestRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Request?> GetByIdWithHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(x => x.Employee)
            .Include(x => x.RequestType)
            .Include(x => x.ApprovalHistory)
                .ThenInclude(x => x.Approver)
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<List<Request>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Request>> GetPendingApprovalsAsync(Guid approverId, CancellationToken cancellationToken = default)
    {
        var approverIdString = approverId.ToString();

        // Database-level filtering using denormalized CurrentStepApproverIds column
        return await _dbSet
            .AsNoTracking()
            .Where(x => (x.Status == RequestStatus.Submitted || x.Status == RequestStatus.InProgress)
                        && x.CurrentStepApproverIds != null
                        && x.CurrentStepApproverIds.Contains(approverIdString))
            .Include(x => x.Employee)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RequestApprovalHistory>> GetApprovalActionsAsync(Guid approverId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<RequestApprovalHistory>()
            .AsNoTracking()
            .Where(h => h.ApproverId == approverId)
            .Include(h => h.Request)
                .ThenInclude(r => r.Employee)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public IQueryable<Request> QueryByEmployeeId(Guid employeeId)
        => _dbSet.AsNoTracking().Where(x => x.EmployeeId == employeeId);

    public IQueryable<Request> QueryByCompanyId(Guid companyId)
        => _dbSet.AsNoTracking().Include(x => x.Employee).Where(x => x.Employee.CompanyId == companyId);

    public IQueryable<Request> QueryPendingApprovals(Guid approverId)
    {
        var approverIdString = approverId.ToString();
        return _dbSet.AsNoTracking()
            .Include(x => x.Employee)
            .Where(x => (x.Status == RequestStatus.Submitted || x.Status == RequestStatus.InProgress)
                        && x.CurrentStepApproverIds != null
                        && x.CurrentStepApproverIds.Contains(approverIdString));
    }

    public IQueryable<RequestApprovalHistory> QueryApprovalActions(Guid approverId)
        => _context.Set<RequestApprovalHistory>()
            .AsNoTracking()
            .Include(h => h.Request)
                .ThenInclude(r => r.Employee)
            .Include(h => h.Request)
                .ThenInclude(r => r.RequestType)
            .Where(h => h.ApproverId == approverId);

    public async Task<int> CountAsync(IQueryable<Request> query, CancellationToken cancellationToken = default)
        => await query.CountAsync(cancellationToken);

    public async Task<List<Request>> ToListAsync(IQueryable<Request> query, CancellationToken cancellationToken = default)
        => await query.ToListAsync(cancellationToken);

    public async Task<int> CountHistoryAsync(IQueryable<RequestApprovalHistory> query, CancellationToken cancellationToken = default)
        => await query.CountAsync(cancellationToken);

    public async Task<List<RequestApprovalHistory>> ToListHistoryAsync(IQueryable<RequestApprovalHistory> query, CancellationToken cancellationToken = default)
        => await query.ToListAsync(cancellationToken);
}
