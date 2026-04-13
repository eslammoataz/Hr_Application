using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class CompanyHierarchyPositionRepository : Repository<CompanyHierarchyPosition>,
    ICompanyHierarchyPositionRepository
{
    public CompanyHierarchyPositionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<CompanyHierarchyPosition>> GetByCompanyAsync(Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CompanyHierarchyPositions
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteAllForCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var existing = await _context.CompanyHierarchyPositions
            .IgnoreQueryFilters()
            .Where(p => p.CompanyId == companyId)
            .ToListAsync(cancellationToken);

        if (existing.Any())
        {
            _context.CompanyHierarchyPositions.RemoveRange(existing);
        }
    }

    public async Task<bool> RoleExistsForCompanyAsync(Guid companyId, UserRole role,
        CancellationToken cancellationToken = default)
    {
        return await _context.CompanyHierarchyPositions
            .AnyAsync(p => p.CompanyId == companyId && p.Role == role, cancellationToken);
    }
}
