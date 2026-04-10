using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class UnitRepository : Repository<Unit>, IUnitRepository
{
    public UnitRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Unit>> GetByDepartmentAsync(Guid departmentId, CancellationToken cancellationToken = default)
        => await _context.Units
            .AsNoTracking()
            .Include(u => u.UnitLeader)
            .Where(u => u.DepartmentId == departmentId && !u.IsDeleted)
            .ToListAsync(cancellationToken);

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
            .Where(u => departmentIds.Contains(u.DepartmentId) && !u.IsDeleted)
            .ToListAsync(cancellationToken);
    }
}
