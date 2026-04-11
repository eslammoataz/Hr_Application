using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class TeamRepository : Repository<Team>, ITeamRepository
{
    public TeamRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
            /// Retrieves all teams that belong to the specified unit, including each team's TeamLeader.
            /// </summary>
            /// <param name="unitId">The identifier of the unit whose teams should be retrieved.</param>
            /// <param name="cancellationToken">Token to observe while waiting for the task to complete.</param>
            /// <returns>A read-only list of teams for the given unit, with each team's TeamLeader loaded; teams are returned regardless of their `IsDeleted` status.</returns>
            public async Task<IReadOnlyList<Team>> GetByUnitAsync(Guid unitId, CancellationToken cancellationToken = default)
        => await _context.Teams
            .AsNoTracking()
            .Include(t => t.TeamLeader)
            .Where(t => t.UnitId == unitId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Retrieves teams whose UnitId is contained in the provided collection and includes each team's TeamLeader.
    /// </summary>
    /// <param name="unitIds">Collection of Unit IDs used to filter teams; if empty, the method returns an empty list.</param>
    /// <param name="cancellationToken">Token to cancel the database query.</param>
    /// <returns>A read-only list of Team entities matching any of the provided unit IDs; empty if none match.</returns>
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
            .Where(t => unitIds.Contains(t.UnitId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
            /// Retrieves a team by its identifier and includes its team leader and members.
            /// </summary>
            /// <param name="id">The team's unique identifier.</param>
            /// <param name="cancellationToken">A token to cancel the database operation.</param>
            /// <returns>The matching Team including its TeamLeader and Members, or null if no team with the specified id exists.</returns>
            public async Task<Team?> GetWithMembersAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Teams
            .Include(t => t.TeamLeader)
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
}
