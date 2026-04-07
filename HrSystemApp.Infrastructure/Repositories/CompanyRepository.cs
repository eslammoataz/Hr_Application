using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;

namespace HrSystemApp.Infrastructure.Repositories;

public class CompanyRepository : Repository<Company>, ICompanyRepository
{
    public CompanyRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Company?> GetWithDetailsAsync(
        Guid id, 
        bool includeLocations = false, 
        bool includeDepartments = false, 
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (includeLocations) query = query.Include(c => c.Locations);
        if (includeDepartments) query = query.Include(c => c.Departments);

        return await query.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<PagedResult<Company>> GetPagedAsync(
        string? searchTerm,
        CompanyStatus? status,
        int pageNumber,
        int pageSize,
        bool includeLocations = false,
        bool includeDepartments = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (includeLocations) query = query.Include(c => c.Locations);
        if (includeDepartments) query = query.Include(c => c.Departments);

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(c => c.CompanyName.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.CompanyName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<Company>.Create(items, pageNumber, pageSize, totalCount);
    }

    public async Task<(int TotalActive, int TotalInactive, int TotalSuspended)> GetStatusCountsAsync(CancellationToken cancellationToken = default)
    {
        var stats = await _dbSet
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Active = g.Count(c => c.Status == CompanyStatus.Active),
                Inactive = g.Count(c => c.Status == CompanyStatus.Inactive),
                Suspended = g.Count(c => c.Status == CompanyStatus.Suspended)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (stats == null)
            return (0, 0, 0);

        return (stats.Active, stats.Inactive, stats.Suspended);
    }
}
