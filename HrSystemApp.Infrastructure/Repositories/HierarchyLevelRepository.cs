using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class HierarchyLevelRepository : Repository<HierarchyLevel>, IHierarchyLevelRepository
{
    public HierarchyLevelRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<HierarchyLevel>> GetAllOrderedAsync(CancellationToken ct)
        => await _context.HierarchyLevels
            .AsNoTracking()
            .OrderBy(l => l.SortOrder)
            .ToListAsync(ct);

    public async Task<bool> HasNodesAsync(Guid levelId, CancellationToken ct)
        => await _context.OrgNodes
            .AsNoTracking()
            .AnyAsync(n => n.LevelId == levelId, ct);
}