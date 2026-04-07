using HrSystemApp.Application.Common;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class ContactAdminRequestRepository : Repository<ContactAdminRequest>, IContactAdminRequestRepository
{
    public ContactAdminRequestRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<bool> ExistsPendingRequestAsync(
        string email,
        string companyName,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<ContactAdminRequest>()
            .AnyAsync(r => r.Status == ContactAdminRequestStatus.Pending &&
                           (r.Email == email || r.CompanyName == companyName || r.PhoneNumber == phoneNumber),
                cancellationToken);
    }

    public async Task<PagedResult<ContactAdminRequest>> GetPagedAsync(
        bool? isAccepted,
        bool? isPending,
        bool? isRejected,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = _dbSet.AsQueryable();

        // Build requested statuses dynamically — handles all combinations
        var statuses = new List<ContactAdminRequestStatus>();

        if (isPending == true) statuses.Add(ContactAdminRequestStatus.Pending);
        if (isAccepted == true) statuses.Add(ContactAdminRequestStatus.Accepted);
        if (isRejected == true) statuses.Add(ContactAdminRequestStatus.Rejected);

        // Only filter if at least one flag was provided, otherwise return all
        if (statuses.Any())
            baseQuery = baseQuery.Where(r => statuses.Contains(r.Status));

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<ContactAdminRequest>.Create(items, pageNumber, pageSize, totalCount);
    }



    public async Task<(int TotalPending, int TotalAccepted, int TotalRejected)> GetStatusCountsAsync(
        CancellationToken cancellationToken = default)
    {
        var stats = await _dbSet
            .GroupBy(_ => 1) // Force a dummy group to perform multiple aggregates in one query
            .Select(g => new
            {
                Pending = g.Count(x => x.Status == ContactAdminRequestStatus.Pending),
                Accepted = g.Count(x => x.Status == ContactAdminRequestStatus.Accepted),
                Rejected = g.Count(x => x.Status == ContactAdminRequestStatus.Rejected)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (stats == null)
            return (0, 0, 0);

        return (stats.Pending, stats.Accepted, stats.Rejected);
    }
}
