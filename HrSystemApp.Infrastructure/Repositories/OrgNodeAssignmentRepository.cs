using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class OrgNodeAssignmentRepository : Repository<OrgNodeAssignment>, IOrgNodeAssignmentRepository
{
    public OrgNodeAssignmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<bool> ExistsAsync(Guid orgNodeId, Guid employeeId, CancellationToken ct)
        => await _context.OrgNodeAssignments
            .AsNoTracking()
            .AnyAsync(a => a.OrgNodeId == orgNodeId && a.EmployeeId == employeeId, ct);

    public async Task<IReadOnlyList<OrgNodeAssignment>> GetByNodeAsync(Guid orgNodeId, CancellationToken ct)
        => await _context.OrgNodeAssignments
            .AsNoTracking()
            .Where(a => a.OrgNodeId == orgNodeId)
            .Include(a => a.Employee)
            .ToListAsync(ct);

    public async Task<OrgNodeAssignment?> GetByNodeAndEmployeeAsync(Guid orgNodeId, Guid employeeId, CancellationToken ct)
        => await _context.OrgNodeAssignments
            .AsNoTracking()
            .Where(a => a.OrgNodeId == orgNodeId && a.EmployeeId == employeeId)
            .FirstOrDefaultAsync(ct);
}