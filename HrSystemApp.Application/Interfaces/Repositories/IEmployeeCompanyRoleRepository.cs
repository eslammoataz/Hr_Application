using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IEmployeeCompanyRoleRepository : IRepository<EmployeeCompanyRole>
{
    Task<IReadOnlyList<EmployeeCompanyRole>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<Employee>> GetActiveEmployeesByRoleIdAsync(Guid roleId, CancellationToken ct = default);
    Task<Dictionary<Guid, IReadOnlyList<Employee>>> GetActiveEmployeesByRoleIdsAsync(IEnumerable<Guid> roleIds, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid employeeId, Guid roleId, CancellationToken ct = default);
    Task RemoveAsync(Guid employeeId, Guid roleId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetPermissionsForEmployeeAsync(Guid employeeId, CancellationToken ct = default);
}
