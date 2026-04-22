using HrSystemApp.Domain.Common;

namespace HrSystemApp.Domain.Models;

/// <summary>
/// A single feature-level permission granted by a CompanyRole.
/// Hard-deleted (not soft-deleted) because replacing a role's permissions
/// means deleting the old set and inserting the new set.
/// </summary>
public class CompanyRolePermission : BaseEntity, IHardDelete
{
    public Guid RoleId { get; set; }

    /// <summary>
    /// One of the string constants defined in AppPermissions.
    /// </summary>
    public string Permission { get; set; } = string.Empty;

    public CompanyRole Role { get; set; } = null!;
}
