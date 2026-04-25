using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class OrgNodeAssignmentRepository : Repository<OrgNodeAssignment>, IOrgNodeAssignmentRepository
{
    public OrgNodeAssignmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<bool> ExistsAsync(Guid orgNodeId, Guid employeeId, CancellationToken ct)
        => await _context.OrgNodeAssignments
            .AnyAsync(a => a.OrgNodeId == orgNodeId && a.EmployeeId == employeeId && !a.IsDeleted, ct);

    public async Task<IReadOnlyList<OrgNodeAssignment>> GetByNodeAsync(Guid orgNodeId, CancellationToken ct)
        => await _context.OrgNodeAssignments
            .Where(a => a.OrgNodeId == orgNodeId && !a.IsDeleted)
            .Include(a => a.Employee)
            .ToListAsync(ct);

    public async Task<OrgNodeAssignment?> GetByNodeAndEmployeeAsync(Guid orgNodeId, Guid employeeId, CancellationToken ct)
        => await _context.OrgNodeAssignments
            .Where(a => a.OrgNodeId == orgNodeId && a.EmployeeId == employeeId && !a.IsDeleted)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<OrgNodeAssignment>> GetByEmployeeAsync(Guid employeeId, CancellationToken ct)
        => await _context.OrgNodeAssignments
            .Where(a => a.EmployeeId == employeeId && !a.IsDeleted)
            .Include(a => a.OrgNode).ThenInclude(n => n.Parent)
            .ToListAsync(ct);

    public async Task<OrgNodeAssignment?> GetByEmployeeWithNodeAsync(Guid employeeId, CancellationToken ct)
        => await _context.OrgNodeAssignments
            .Where(a => a.EmployeeId == employeeId && !a.IsDeleted)
            .Include(a => a.OrgNode)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Employee>> GetManagersByNodeAsync(Guid orgNodeId, CancellationToken ct)
        => await _context.OrgNodeAssignments
            .Where(a => a.OrgNodeId == orgNodeId && a.Role == OrgRole.Manager && !a.IsDeleted)
            .Include(a => a.Employee)
            .Select(a => a.Employee)
            .ToListAsync(ct);

    public async Task<Dictionary<Guid, IReadOnlyList<Employee>>> GetManagersByNodesAsync(IEnumerable<Guid> orgNodeIds, CancellationToken ct)
    {
        var orgNodeIdList = orgNodeIds.ToList();
        if (orgNodeIdList.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<Employee>>();

        var assignments = await _context.OrgNodeAssignments
            .Where(a => orgNodeIdList.Contains(a.OrgNodeId) && a.Role == OrgRole.Manager && !a.IsDeleted)
            .Include(a => a.Employee)
            .ToListAsync(ct);

        return assignments
            .GroupBy(a => a.OrgNodeId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Employee>)g.Select(a => a.Employee).ToList());
    }

    public async Task<bool> IsManagerAtNodeAsync(Guid employeeId, Guid orgNodeId, CancellationToken ct)
        => await _context.OrgNodeAssignments
            .AnyAsync(a => a.EmployeeId == employeeId
                        && a.OrgNodeId == orgNodeId
                        && a.Role == OrgRole.Manager, ct);
}
