using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Services;

/// <summary>
/// Service responsible for reading and validating the company organizational hierarchy.
/// </summary>
public interface IHierarchyService
{
    /// <summary>
    /// Returns all UserRoles configured in the company's hierarchy positions, ordered by SortOrder.
    /// </summary>
    Task<List<UserRole>> GetAvailableRolesAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>
    /// Returns true if ALL provided roles are present in the company's configured hierarchy.
    /// Used to validate Request Definition workflow steps.
    /// </summary>
    Task<bool> AreRolesValidForCompanyAsync(Guid companyId, IEnumerable<UserRole> roles, CancellationToken ct = default);

    /// <summary>
    /// Walks up the organizational tree from the employee and returns the ordered chain
    /// of head-employees (TeamLeader → UnitLeader → DeptManager → VP → CEO).
    /// </summary>
    Task<List<Employee>> GetEmployeeHierarchyPathAsync(Guid employeeId, CancellationToken ct = default);
}
