using HrSystemApp.Domain.Models;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface ICompanyRepository : IRepository<Company>
{
    Task<Company?> GetWithDetailsAsync(Guid id, bool includeLocations = false, CancellationToken cancellationToken = default);
    Task<CompaniesPagedResult> GetPagedAsync(
        string? searchTerm,
        CompanyStatus? status,
        int pageNumber,
        int pageSize,
        bool includeLocations = false,
        CancellationToken cancellationToken = default);


}
