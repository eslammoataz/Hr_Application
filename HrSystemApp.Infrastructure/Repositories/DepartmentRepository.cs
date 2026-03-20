using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class DepartmentRepository : Repository<Department>, IDepartmentRepository
{
    public DepartmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Department?> GetWithUnitsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Departments
            .Include(d => d.VicePresident)
            .Include(d => d.Manager)
            .Include(d => d.Units)
                .ThenInclude(u => u.UnitLeader)
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<Department>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => await _context.Departments
            .Include(d => d.VicePresident)
            .Include(d => d.Manager)
            .Where(d => d.CompanyId == companyId && !d.IsDeleted)
            .ToListAsync(cancellationToken);
}
