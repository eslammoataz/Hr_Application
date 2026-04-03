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
        var query = _dbSet.AsQueryable();

        // Build requested statuses dynamically — handles all combinations
        var statuses = new List<ContactAdminRequestStatus>();

        if (isPending == true) statuses.Add(ContactAdminRequestStatus.Pending);
        if (isAccepted == true) statuses.Add(ContactAdminRequestStatus.Accepted);
        if (isRejected == true) statuses.Add(ContactAdminRequestStatus.Rejected);

        // Only filter if at least one flag was provided, otherwise return all
        if (statuses.Any())
            query = query.Where(r => statuses.Contains(r.Status));

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<ContactAdminRequest>.Create(items, pageNumber, pageSize, totalCount);
    }
}