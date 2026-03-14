using HrSystemApp.Domain.Models;
namespace HrSystemApp.Application.Interfaces.Repositories;

/// <summary>
/// Employee repository interface
/// </summary>
public interface IEmployeeRepository : IRepository<Employee>
{
    Task<Employee?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
