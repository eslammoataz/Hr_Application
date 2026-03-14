using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IDepartmentRepository : IRepository<Department>
{
    Task<Department?> GetWithUnitsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Department>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
}
