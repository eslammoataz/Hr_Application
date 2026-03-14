namespace HrSystemApp.Domain.Models;

public class Department : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? VicePresidentId { get; set; }
    public Guid? ManagerId { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
    public Employee? VicePresident { get; set; }
    public Employee? Manager { get; set; }
    public ICollection<Unit> Units { get; set; } = new List<Unit>();
}