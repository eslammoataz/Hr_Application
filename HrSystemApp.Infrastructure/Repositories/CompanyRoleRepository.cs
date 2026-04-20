using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class CompanyRoleRepository : Repository<CompanyRole>, ICompanyRoleRepository
{
    public CompanyRoleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<CompanyRole?> GetWithPermissionsAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<IReadOnlyList<CompanyRole>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(r => r.Permissions)
            .Where(r => r.CompanyId == companyId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? excludeId, CancellationToken ct = default)
    {
        var query = _dbSet.Where(r => r.CompanyId == companyId
                                   && r.Name.ToLower() == name.ToLower());
        if (excludeId.HasValue)
            query = query.Where(r => r.Id != excludeId.Value);

        return await query.AnyAsync(ct);
    }

    public async Task ClearPermissionsAsync(Guid roleId, CancellationToken ct = default)
    {
        var permissions = await _context.Set<CompanyRolePermission>()
            .Where(p => p.RoleId == roleId)
            .ToListAsync(ct);
        _context.Set<CompanyRolePermission>().RemoveRange(permissions);
    }

    public async Task ReplacePermissionsAsync(Guid roleId, IEnumerable<string> permissions, CancellationToken ct = default)
    {
        var permsToRemove = await _context.Set<CompanyRolePermission>()
            .Where(p => p.RoleId == roleId)
            .ToListAsync(ct);
        _context.Set<CompanyRolePermission>().RemoveRange(permsToRemove);

        var newPerms = permissions.Distinct()
            .Select(p => new CompanyRolePermission { RoleId = roleId, Permission = p });
        await _context.Set<CompanyRolePermission>().AddRangeAsync(newPerms, ct);
    }
}
