using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class UnitRepository : Repository<Unit>, IUnitRepository
{
    public UnitRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
            /// Retrieves all units that belong to the specified department.
            /// </summary>
            /// <returns>A read-only list of Unit entities whose DepartmentId equals the provided departmentId; an empty list if no matches are found.</returns>
            public async Task<IReadOnlyList<Unit>> GetByDepartmentAsync(Guid departmentId, CancellationToken cancellationToken = default)
        => await _context.Units
            .AsNoTracking()
            .Include(u => u.UnitLeader)
            .Where(u => u.DepartmentId == departmentId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Retrieves units whose DepartmentId is contained in the provided collection.
    /// </summary>
    /// <param name="departmentIds">Collection of department IDs to match; if empty, the method returns an empty list.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the database query.</param>
    /// <returns>A read-only list of matching <see cref="Unit"/> entities; empty if no units match or when <paramref name="departmentIds"/> is empty.</returns>
    public async Task<IReadOnlyList<Unit>> GetByDepartmentIdsAsync(
        IReadOnlyCollection<Guid> departmentIds,
        CancellationToken cancellationToken = default)
    {
        if (departmentIds.Count == 0)
        {
            return Array.Empty<Unit>();
        }

        return await _context.Units
            .AsNoTracking()
            .Include(u => u.UnitLeader)
            .Where(u => departmentIds.Contains(u.DepartmentId))
            .ToListAsync(cancellationToken);
    }
}
