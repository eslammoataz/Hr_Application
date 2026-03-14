using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Domain.Models;

/// <summary>
/// Defines a role's position in the company hierarchy.
/// e.g. CEO (SortOrder=1), VP (SortOrder=2), DepartmentManager (SortOrder=3) ...
/// </summary>
public class CompanyHierarchyPosition : BaseEntity
{
    public Guid CompanyId { get; set; }

    /// <summary>The role this position represents</summary>
    public UserRole Role { get; set; }

    /// <summary>Display name for the position (e.g. "Chief Executive Officer")</summary>
    public string PositionTitle { get; set; } = string.Empty;

    /// <summary>1 = top of hierarchy, higher numbers = lower levels</summary>
    public int SortOrder { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
}