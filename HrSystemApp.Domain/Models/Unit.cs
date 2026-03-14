namespace HrSystemApp.Domain.Models;

public class Unit : AuditableEntity
{
    public Guid DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? UnitLeaderId { get; set; }

    // Navigation
    public Department Department { get; set; } = null!;
    public Employee? UnitLeader { get; set; }
    public ICollection<Team> Teams { get; set; } = new List<Team>();
}