using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IUnitRepository : IRepository<Unit>
{
    Task<IReadOnlyList<Unit>> GetByDepartmentAsync(Guid departmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Unit>> GetByDepartmentIdsAsync(
        IReadOnlyCollection<Guid> departmentIds,
        CancellationToken cancellationToken = default);
}
