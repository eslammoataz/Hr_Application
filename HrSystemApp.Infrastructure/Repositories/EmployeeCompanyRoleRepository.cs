using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class EmployeeCompanyRoleRepository : Repository<EmployeeCompanyRole>, IEmployeeCompanyRoleRepository
{
    public EmployeeCompanyRoleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<EmployeeCompanyRole>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(er => er.Role)
                .ThenInclude(r => r.Permissions)
            .Where(er => er.EmployeeId == employeeId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Employee>> GetActiveEmployeesByRoleIdAsync(Guid roleId, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(er => er.Employee)
            .Where(er => er.RoleId == roleId
                      && er.Employee.EmploymentStatus == EmploymentStatus.Active)
            .Select(er => er.Employee)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(Guid employeeId, Guid roleId, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(er => er.EmployeeId == employeeId && er.RoleId == roleId, ct);
    }

    public async Task RemoveAsync(Guid employeeId, Guid roleId, CancellationToken ct = default)
    {
        var assignment = await _dbSet
            .FirstOrDefaultAsync(er => er.EmployeeId == employeeId && er.RoleId == roleId, ct);
        if (assignment is not null)
            _dbSet.Remove(assignment);
    }

    public async Task<IReadOnlyList<string>> GetPermissionsForEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(er => er.EmployeeId == employeeId)
            .SelectMany(er => er.Role.Permissions.Select(p => p.Permission))
            .Distinct()
            .ToListAsync(ct);
    }
}
