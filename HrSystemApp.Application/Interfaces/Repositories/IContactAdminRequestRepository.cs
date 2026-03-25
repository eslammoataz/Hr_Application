using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IContactAdminRequestRepository : IRepository<ContactAdminRequest>
{
    Task<bool> ExistsPendingRequestAsync(string email, string companyName,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ContactAdminRequest>> GetPagedAsync(
        bool? isAccepted,
        bool? isPending,
        bool? isRejected,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
