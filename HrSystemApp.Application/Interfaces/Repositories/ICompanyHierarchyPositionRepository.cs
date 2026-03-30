using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface ICompanyHierarchyPositionRepository : IRepository<CompanyHierarchyPosition>
{
    /// <summary>Returns all positions for a company, ordered by SortOrder.</summary>
    Task<IReadOnlyList<CompanyHierarchyPosition>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>Deletes all existing positions for a company (used before replacement).</summary>
    Task DeleteAllForCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>Returns true if a specific role is configured for the company.</summary>
    Task<bool> RoleExistsForCompanyAsync(Guid companyId, UserRole role, CancellationToken cancellationToken = default);
}
