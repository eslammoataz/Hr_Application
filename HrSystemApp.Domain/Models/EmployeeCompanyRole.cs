using HrSystemApp.Domain.Common;

namespace HrSystemApp.Domain.Models;

/// <summary>
/// Junction table: assigns a CompanyRole to an Employee (many-to-many).
/// Hard-deleted on removal because "unassignment" means the row should disappear.
/// </summary>
public class EmployeeCompanyRole : BaseEntity, IHardDelete
{
    public Guid EmployeeId { get; set; }
    public Guid RoleId { get; set; }
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;

    public Employee Employee { get; set; } = null!;
    public CompanyRole Role { get; set; } = null!;
}
