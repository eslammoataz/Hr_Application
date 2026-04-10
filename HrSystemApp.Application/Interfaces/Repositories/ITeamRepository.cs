using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface ITeamRepository : IRepository<Team>
{
    Task<IReadOnlyList<Team>> GetByUnitAsync(Guid unitId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Team>> GetByUnitIdsAsync(
        IReadOnlyCollection<Guid> unitIds,
        CancellationToken cancellationToken = default);
    Task<Team?> GetWithMembersAsync(Guid id, CancellationToken cancellationToken = default);
}
