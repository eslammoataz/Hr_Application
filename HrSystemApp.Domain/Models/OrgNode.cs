namespace HrSystemApp.Domain.Models;

public class OrgNode : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public string? Type { get; set; }

    // Navigation
    public OrgNode? Parent { get; set; }
    public ICollection<OrgNode> Children { get; set; } = new List<OrgNode>();
    public ICollection<OrgNodeAssignment> Assignments { get; set; } = new List<OrgNodeAssignment>();
}
