using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class TeamRepository : Repository<Team>, ITeamRepository
{
    public TeamRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Team>> GetByUnitAsync(Guid unitId, CancellationToken cancellationToken = default)
        => await _context.Teams
            .AsNoTracking()
            .Include(t => t.TeamLeader)
            .Where(t => t.UnitId == unitId && !t.IsDeleted)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Team>> GetByUnitIdsAsync(
        IReadOnlyCollection<Guid> unitIds,
        CancellationToken cancellationToken = default)
    {
        if (unitIds.Count == 0)
        {
            return Array.Empty<Team>();
        }

        return await _context.Teams
            .AsNoTracking()
            .Include(t => t.TeamLeader)
            .Where(t => unitIds.Contains(t.UnitId) && !t.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<Team?> GetWithMembersAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Teams
            .AsNoTracking()
            .Include(t => t.TeamLeader)
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, cancellationToken);
}
