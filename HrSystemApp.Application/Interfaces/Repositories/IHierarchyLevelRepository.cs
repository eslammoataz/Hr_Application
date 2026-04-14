using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IHierarchyLevelRepository : IRepository<HierarchyLevel>
{
    Task<IReadOnlyList<HierarchyLevel>> GetAllOrderedAsync(CancellationToken ct);
    Task<bool> HasNodesAsync(Guid levelId, CancellationToken ct);
}