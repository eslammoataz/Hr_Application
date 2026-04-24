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

    /// <summary>
    /// Replaces all permissions for the specified role with the provided permission strings.
    /// </summary>
    /// <param name="roleId">The identifier of the role whose permissions will be replaced.</param>
    /// <param name="permissions">The permission values to assign to the role; duplicates are ignored.</param>
    /// <param name="ct">A token to observe while waiting for the operation to complete.</param>
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

    /// <summary>
            /// Retrieves the CompanyRole entities matching the supplied role IDs without change tracking and returns them keyed by their Id.
            /// </summary>
            /// <param name="ids">Collection of role identifiers to fetch. Only roles that exist are included in the returned dictionary.</param>
            /// <param name="ct">Token to observe while waiting for the task to complete.</param>
            /// <returns>A dictionary mapping each found role's Id to its corresponding CompanyRole.</returns>
            public async Task<Dictionary<Guid, CompanyRole>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        => await _dbSet.AsNoTracking()
            .Where(r => ids.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);
}
