using HrSystemApp.Domain.Enums;


namespace HrSystemApp.Domain.Models;

public class OrgNode : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Guid? LevelId { get; set; }
    public Guid? EntityId { get; set; }
    public OrgEntityType? EntityType { get; set; }

    // Navigation
    public OrgNode? Parent { get; set; }
    public ICollection<OrgNode> Children { get; set; } = new List<OrgNode>();
    public HierarchyLevel? Level { get; set; }
    public ICollection<OrgNodeAssignment> Assignments { get; set; } = new List<OrgNodeAssignment>();
}

