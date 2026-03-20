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
            .Include(u => u.UnitLeader)
            .Where(u => u.DepartmentId == departmentId && !u.IsDeleted)
            .ToListAsync(cancellationToken);
}
