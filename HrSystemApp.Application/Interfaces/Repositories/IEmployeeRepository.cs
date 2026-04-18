using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IEmployeeRepository : IRepository<Employee>
{
    Task<Employee?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<Employee?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeProfileDto?> GetProfileByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Employee>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<EmployeesPagedResult> GetPagedForListAsync(
        Guid? companyId,
        string? searchTerm,
        UserRole? role,
        EmploymentStatus? employmentStatus,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
