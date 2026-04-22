using HrSystemApp.Domain.Common;

namespace HrSystemApp.Domain.Models;

/// <summary>
/// A custom, named role scoped to a company. Employees can be assigned to it,
/// and workflow approval steps can require it.
/// Soft-deleted automatically by BaseEntity conventions — do not hard-delete.
/// </summary>
public class CompanyRole : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Company Company { get; set; } = null!;
    public ICollection<CompanyRolePermission> Permissions { get; set; } = new List<CompanyRolePermission>();
    public ICollection<EmployeeCompanyRole> EmployeeRoles { get; set; } = new List<EmployeeCompanyRole>();
}
