using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface ICompanyRoleRepository : IRepository<CompanyRole>
{
    Task<CompanyRole?> GetWithPermissionsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CompanyRole>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? excludeId, CancellationToken ct = default);
    Task ClearPermissionsAsync(Guid roleId, CancellationToken ct = default);
    Task ReplacePermissionsAsync(Guid roleId, IEnumerable<string> permissions, CancellationToken ct = default);

    Task<Dictionary<Guid, CompanyRole>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}
