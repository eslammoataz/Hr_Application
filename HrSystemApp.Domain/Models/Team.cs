namespace HrSystemApp.Domain.Models;

public class Team : AuditableEntity
{
    public Guid UnitId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? TeamLeaderId { get; set; }

    // Navigation
    public Unit Unit { get; set; } = null!;
    public Employee? TeamLeader { get; set; }
    public ICollection<Employee> Members { get; set; } = new List<Employee>();
}