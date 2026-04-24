using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IRequestRepository : IRepository<Request>
{
    /// <summary>
/// Fetches the request with the specified identifier including its approval history.
/// </summary>
/// <param name="id">The identifier of the request to retrieve.</param>
/// <returns>The <see cref="Request"/> including its associated approval history, or <c>null</c> if no matching request exists.</returns>
Task<Request?> GetByIdWithHistoryAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>
/// Retrieves all Request records belonging to the specified employee.
/// </summary>
/// <param name="employeeId">The identifier of the employee whose requests to retrieve.</param>
/// <returns>A list of Request entities for the employee; empty if none are found.</returns>
Task<List<Request>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    /// <summary>
/// Retrieves requests that are awaiting approval from the specified approver.
/// </summary>
/// <param name="approverId">The identifier of the approver whose pending approvals to retrieve.</param>
/// <returns>A list of Request entities pending approval by the specified approver; an empty list if none are found.</returns>
Task<List<Request>> GetPendingApprovalsAsync(Guid approverId, CancellationToken cancellationToken = default);
    /// <summary>
/// Retrieves approval-history entries associated with the specified approver.
/// </summary>
/// <param name="approverId">Identifier of the approver whose approval actions to retrieve.</param>
/// <returns>List of RequestApprovalHistory entries for the given approver; empty list if none exist.</returns>
Task<List<RequestApprovalHistory>> GetApprovalActionsAsync(Guid approverId, CancellationToken cancellationToken = default);

    /// <summary>
/// Gets a queryable sequence of Request entities for the specified employee.
/// </summary>
/// <param name="employeeId">The employee's unique identifier to filter requests by.</param>
/// <returns>An <see cref="IQueryable{Request}"/> that yields Request entities belonging to the specified employee.</returns>
IQueryable<Request> QueryByEmployeeId(Guid employeeId);
    /// <summary>
/// Provides a queryable sequence of requests belonging to the specified company.
/// </summary>
/// <param name="companyId">The identifier of the company whose requests to filter.</param>
/// <returns>An IQueryable of Request containing requests for the given company.</returns>
IQueryable<Request> QueryByCompanyId(Guid companyId);
    /// <summary>
/// Gets a queryable sequence of requests pending approval by the specified approver.
/// </summary>
/// <param name="approverId">The identifier of the approver whose pending approvals to query.</param>
/// <returns>A queryable sequence of requests that are pending approval by the specified approver.</returns>
IQueryable<Request> QueryPendingApprovals(Guid approverId);
    /// <summary>
/// Gets a queryable sequence of approval history entries for the specified approver.
/// </summary>
/// <param name="approverId">The identifier of the approver whose approval history to query.</param>
/// <returns>An <see cref="IQueryable{RequestApprovalHistory}"/> filtered to approval history entries for the given approver.</returns>
IQueryable<RequestApprovalHistory> QueryApprovalActions(Guid approverId);

    /// <summary>
/// Executes a count against the provided queryable source of Request entities.
/// </summary>
/// <param name="query">An <see cref="IQueryable{Request}"/> representing the filtered set of requests to count.</param>
/// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
/// <returns>The number of Request entities that match the query.</returns>
Task<int> CountAsync(IQueryable<Request> query, CancellationToken cancellationToken = default);
    /// <summary>
/// Materializes the provided request query into a list.
/// </summary>
/// <param name="query">The query that produces the requests to materialize.</param>
/// <param name="cancellationToken">A token to cancel the operation.</param>
/// <returns>A list of Request containing the results of the executed query.</returns>
Task<List<Request>> ToListAsync(IQueryable<Request> query, CancellationToken cancellationToken = default);
    /// <summary>
/// Counts the approval-history entries represented by the provided query.
/// </summary>
/// <param name="query">Queryable source of <see cref="RequestApprovalHistory"/> to count.</param>
/// <param name="cancellationToken">Token to observe for cancellation.</param>
/// <returns>The number of approval-history entries matching the query.</returns>
Task<int> CountHistoryAsync(IQueryable<RequestApprovalHistory> query, CancellationToken cancellationToken = default);
    /// <summary>
/// Materializes an approval-history query into a list.
/// </summary>
/// <param name="query">The approval-history query to execute and materialize.</param>
/// <param name="cancellationToken">A token to cancel the operation.</param>
/// <returns>A list containing the materialized <see cref="RequestApprovalHistory"/> entities from the provided query.</returns>
Task<List<RequestApprovalHistory>> ToListHistoryAsync(IQueryable<RequestApprovalHistory> query, CancellationToken cancellationToken = default);
}
