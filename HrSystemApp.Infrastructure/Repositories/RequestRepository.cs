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

    /// <summary>
    /// Retrieves approval history entries for the specified approver, including each entry's Request and that Request's Employee, ordered by newest first.
    /// </summary>
    /// <param name="approverId">The approver's identifier whose approval actions to retrieve.</param>
    /// <returns>A list of RequestApprovalHistory records for the approver, each including its Request and the Request's Employee, ordered by CreatedAt descending.</returns>
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

    /// <summary>
        /// Gets a queryable sequence of requests for the specified employee with change tracking disabled.
        /// </summary>
        /// <param name="employeeId">The identifier of the employee whose requests to query.</param>
        /// <returns>A queryable sequence of Request entities filtered by the specified employee identifier.</returns>
        public IQueryable<Request> QueryByEmployeeId(Guid employeeId)
        => _dbSet.AsNoTracking().Where(x => x.EmployeeId == employeeId);

    /// <summary>
        /// Gets requests for employees belonging to a specific company.
        /// </summary>
        /// <param name="companyId">The company identifier used to filter requests by matching Request.Employee.CompanyId.</param>
        /// <returns>An <see cref="IQueryable{Request}"/> of requests whose employee's CompanyId equals the provided <paramref name="companyId"/>, with <see cref="Request.Employee"/> included and change tracking disabled.</returns>
        public IQueryable<Request> QueryByCompanyId(Guid companyId)
        => _dbSet.AsNoTracking().Include(x => x.Employee).Where(x => x.Employee.CompanyId == companyId);

    /// <summary>
    /// Builds a query for requests that are pending approval by the specified approver.
    /// </summary>
    /// <param name="approverId">The approver's Id used to filter requests whose current step approver IDs include this value.</param>
    /// <returns>An <see cref="IQueryable{Request}"/> filtered to requests with status Submitted or InProgress whose current step approver IDs contain the provided approver id.</returns>
    public IQueryable<Request> QueryPendingApprovals(Guid approverId)
    {
        var approverIdString = approverId.ToString();
        return _dbSet.AsNoTracking()
            .Include(x => x.Employee)
            .Where(x => (x.Status == RequestStatus.Submitted || x.Status == RequestStatus.InProgress)
                        && x.CurrentStepApproverIds != null
                        && x.CurrentStepApproverIds.Contains(approverIdString));
    }

    /// <summary>
            /// Builds a query for approval history entries for the specified approver and includes each entry's Request and the Request's Employee.
            /// </summary>
            /// <param name="approverId">The identifier of the approver whose approval history to query.</param>
            /// <returns>An <see cref="IQueryable{RequestApprovalHistory}"/> filtered to entries with <c>ApproverId == approverId</c>, with related <c>Request</c> and <c>Request.Employee</c> included.</returns>
            public IQueryable<RequestApprovalHistory> QueryApprovalActions(Guid approverId)
        => _context.Set<RequestApprovalHistory>()
            .AsNoTracking()
            .Include(h => h.Request)
                .ThenInclude(r => r.Employee)
            .Where(h => h.ApproverId == approverId);

    /// <summary>
        /// Counts the Request entities represented by the given query.
        /// </summary>
        /// <param name="query">An IQueryable of Request used to determine which records to count.</param>
        /// <param name="cancellationToken">A token to cancel the count operation.</param>
        /// <returns>The number of Request records that match the query.</returns>
        public async Task<int> CountAsync(IQueryable<Request> query, CancellationToken cancellationToken = default)
        => await query.CountAsync(cancellationToken);

    /// <summary>
        /// Materializes the provided request query into a List of Request entities.
        /// </summary>
        /// <param name="query">The query that will be executed to produce requests.</param>
        /// <param name="cancellationToken">Token to observe while waiting for the task to complete.</param>
        /// <returns>The list of requests produced by executing the query.</returns>
        public async Task<List<Request>> ToListAsync(IQueryable<Request> query, CancellationToken cancellationToken = default)
        => await query.ToListAsync(cancellationToken);

    /// <summary>
        /// Counts RequestApprovalHistory records represented by the provided query.
        /// </summary>
        /// <param name="query">An <see cref="IQueryable{RequestApprovalHistory}"/> that selects the history records to count.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The number of records that match the query.</returns>
        public async Task<int> CountHistoryAsync(IQueryable<RequestApprovalHistory> query, CancellationToken cancellationToken = default)
        => await query.CountAsync(cancellationToken);

    /// <summary>
        /// Materializes the specified approval-history query into a list.
        /// </summary>
        /// <param name="query">The LINQ query that produces RequestApprovalHistory entities.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>A list of RequestApprovalHistory objects produced by the query.</returns>
        public async Task<List<RequestApprovalHistory>> ToListHistoryAsync(IQueryable<RequestApprovalHistory> query, CancellationToken cancellationToken = default)
        => await query.ToListAsync(cancellationToken);
}
