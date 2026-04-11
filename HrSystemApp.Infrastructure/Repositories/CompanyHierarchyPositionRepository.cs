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

    /// <summary>
    /// Retrieves all hierarchy positions for the specified company, ordered by <c>SortOrder</c>, including records regardless of soft-delete state.
    /// </summary>
    /// <param name="companyId">The identifier of the company whose positions to retrieve.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>A read-only list of <see cref="CompanyHierarchyPosition"/> for the specified company ordered by <c>SortOrder</c>.</returns>
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

    /// <summary>
    /// Determines whether any CompanyHierarchyPosition exists for the specified company with the specified role, including soft-deleted records.
    /// </summary>
    /// <param name="companyId">The company identifier to check.</param>
    /// <param name="role">The user role to check for.</param>
    /// <returns>true if a matching CompanyHierarchyPosition exists, false otherwise.</returns>
    public async Task<bool> RoleExistsForCompanyAsync(Guid companyId, UserRole role,
        CancellationToken cancellationToken = default)
    {
        return await _context.CompanyHierarchyPositions
            .AnyAsync(p => p.CompanyId == companyId && p.Role == role, cancellationToken);
    }
}
