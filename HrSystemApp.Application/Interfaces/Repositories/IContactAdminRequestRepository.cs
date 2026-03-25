using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IContactAdminRequestRepository : IRepository<ContactAdminRequest>
{
    Task<bool> ExistsPendingRequestAsync(string email, string companyName, CancellationToken cancellationToken = default);
    Task<PagedResult<ContactAdminRequest>> GetPagedAsync(
        ContactAdminRequestStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
