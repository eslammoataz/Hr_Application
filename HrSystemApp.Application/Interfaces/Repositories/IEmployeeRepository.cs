using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IEmployeeRepository : IRepository<Employee>
{
    Task<Employee?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<Employee?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>
/// Retrieves the projected employee profile for the specified user ID.
/// </summary>
/// <param name="userId">The user ID whose employee profile should be retrieved.</param>
/// <returns>The <see cref="EmployeeProfileDto"/> for the user, or `null` if no matching employee exists.</returns>
Task<EmployeeProfileDto?> GetProfileByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    /// <summary>
/// Retrieves all employees associated with the specified company.
/// </summary>
/// <param name="companyId">The identifier of the company whose employees to retrieve.</param>
/// <param name="cancellationToken">Token to cancel the operation.</param>
/// <returns>An <see cref="IReadOnlyList{Employee}"/> containing employees for the company; empty if none are found.</returns>
Task<IReadOnlyList<Employee>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
/// Retrieves a paged list of employees for use in listing views, applying the provided optional filters.
/// </summary>
/// <param name="companyId">If provided, restricts results to employees belonging to the specified company.</param>
/// <param name="searchTerm">Optional text used to filter employees by relevant fields such as name or email.</param>
/// <param name="role">Optional user role to filter the results.</param>
/// <param name="employmentStatus">Optional employment status to filter the results.</param>
/// <param name="pageNumber">The page number to return.</param>
/// <param name="pageSize">The number of items per page.</param>
/// <returns>An EmployeesPagedResult containing the matching employees and pagination metadata.</returns>
Task<EmployeesPagedResult> GetPagedForListAsync(Guid? companyId, string? searchTerm, UserRole? role, EmploymentStatus? employmentStatus, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    /// <summary>
/// Retrieves employees for the specified collection of IDs and returns them keyed by their Id.
/// </summary>
/// <param name="ids">Collection of employee IDs to retrieve.</param>
/// <returns>A dictionary mapping each found employee's Id to the corresponding <see cref="Employee"/>; IDs with no match are omitted (the dictionary may be empty).</returns>
Task<Dictionary<Guid, Employee>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}
