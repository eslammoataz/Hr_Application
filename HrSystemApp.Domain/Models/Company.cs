using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Domain.Models;

public class Company : AuditableEntity
{
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLogoUrl { get; set; }
    public int YearlyVacationDays { get; set; } = 21;
    public TimeSpan StartTime { get; set; } = new(9, 0, 0);
    public TimeSpan EndTime { get; set; } = new(17, 0, 0);
    public int GraceMinutes { get; set; } = 15;
    public string TimeZoneId { get; set; } = "UTC";
    public CompanyStatus Status { get; set; } = CompanyStatus.Active;

    // Navigation
    public ICollection<CompanyLocation> Locations { get; set; } = new List<CompanyLocation>();
    public ICollection<CompanyHierarchyPosition> HierarchyPositions { get; set; } = new List<CompanyHierarchyPosition>();
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
