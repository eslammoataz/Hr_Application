using HrSystemApp.Domain.Enums;

public class CompanyHierarchyPosition : BaseEntity
{
    public Guid CompanyId { get; set; }
    public UserRole Role { get; set; }
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; } = true;

    // Navigation
    public Company Company { get; set; } = null!;
}