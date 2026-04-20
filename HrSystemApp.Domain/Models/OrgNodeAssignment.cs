using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Domain.Models;

public class OrgNodeAssignment : BaseEntity
{
    public Guid OrgNodeId { get; set; }
    public Guid EmployeeId { get; set; }
    public OrgRole Role { get; set; }

    // Navigation
    public OrgNode OrgNode { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
