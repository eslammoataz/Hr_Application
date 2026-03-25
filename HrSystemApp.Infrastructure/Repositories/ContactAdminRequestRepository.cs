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

    public async Task<bool> ExistsPendingRequestAsync(string email, string companyName, CancellationToken cancellationToken = default)
    {
        return await _context.Set<ContactAdminRequest>()
            .AnyAsync(r => r.Status == ContactAdminRequestStatus.Pending && 
                          (r.Email == email || r.CompanyName == companyName), 
                cancellationToken);
    }

    public async Task<PagedResult<ContactAdminRequest>> GetPagedAsync(
        ContactAdminRequestStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<ContactAdminRequest>.Create(items, pageNumber, pageSize, totalCount);
    }
}
