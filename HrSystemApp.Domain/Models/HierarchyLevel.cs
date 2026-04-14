namespace HrSystemApp.Domain.Models;

public class HierarchyLevel : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public Guid? ParentLevelId { get; set; }

    // Navigation
    public HierarchyLevel? ParentLevel { get; set; }
    public ICollection<HierarchyLevel> ChildLevels { get; set; } = new List<HierarchyLevel>();
    public ICollection<OrgNode> Nodes { get; set; } = new List<OrgNode>();
}