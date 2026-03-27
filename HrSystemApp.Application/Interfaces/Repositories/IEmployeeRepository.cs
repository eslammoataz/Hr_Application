using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IEmployeeRepository : IRepository<Employee>
{
    Task<Employee?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<Employee?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeProfileDto?> GetProfileByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<PagedResult<Employee>> GetPagedAsync(Guid? companyId, Guid? teamId, string? searchTerm, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
