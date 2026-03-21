using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface ICompanyRepository : IRepository<Company>
{
    Task<Company?> GetWithDetailsAsync(Guid id, bool includeLocations = false, bool includeDepartments = false, CancellationToken cancellationToken = default);
    Task<HrSystemApp.Application.Common.PagedResult<Company>> GetPagedAsync(
        string? searchTerm,
        int pageNumber,
        int pageSize,
        bool includeLocations = false,
        bool includeDepartments = false,
        CancellationToken cancellationToken = default);
}
