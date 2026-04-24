using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface ICompanyRoleRepository : IRepository<CompanyRole>
{
    Task<CompanyRole?> GetWithPermissionsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CompanyRole>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);
    /// <summary>
/// Determines whether a role name already exists within the specified company.
/// </summary>
/// <param name="companyId">The identifier of the company to search within.</param>
/// <param name="name">The role name to check for existence.</param>
/// <param name="excludeId">An optional role identifier to exclude from the check (useful when updating an existing role).</param>
/// <param name="ct">A cancellation token.</param>
/// <returns>`true` if a role with the specified name exists for the company (excluding <paramref name="excludeId"/> if provided), `false` otherwise.</returns>
Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? excludeId, CancellationToken ct = default);
    /// <summary>
/// Removes all permissions associated with the specified role.
/// </summary>
/// <param name="roleId">The identifier of the role whose permissions will be cleared.</param>
/// <param name="ct">A token to observe while waiting for the operation to complete.</param>
Task ClearPermissionsAsync(Guid roleId, CancellationToken ct = default);
    /// <summary>
/// Replaces the permissions assigned to the specified role with the provided set.
/// </summary>
/// <param name="roleId">The identifier of the role whose permissions will be replaced.</param>
/// <param name="permissions">The new collection of permission identifiers or names to assign to the role.</param>
/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
Task ReplacePermissionsAsync(Guid roleId, IEnumerable<string> permissions, CancellationToken ct = default);

    /// <summary>
/// Retrieves CompanyRole entities for the specified role IDs.
/// </summary>
/// <param name="ids">The role IDs to fetch.</param>
/// <param name="ct">A cancellation token.</param>
/// <returns>A dictionary mapping each found role ID to its corresponding <see cref="CompanyRole"/>; IDs without a matching role are omitted.</returns>
Task<Dictionary<Guid, CompanyRole>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}
