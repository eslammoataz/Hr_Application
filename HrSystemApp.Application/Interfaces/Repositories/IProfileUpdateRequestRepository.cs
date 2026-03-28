using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees.ProfileUpdateRequests;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IProfileUpdateRequestRepository : IRepository<ProfileUpdateRequest>
{
    Task<PagedResult<ProfileUpdateRequestDto>> GetPagedRequestsByCompanyAsync(Guid companyId, ProfileUpdateRequestStatus? status,
        int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task<PagedResult<ProfileUpdateRequestDto>> GetPagedMyRequestsAsync(Guid employeeId, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default);
}
