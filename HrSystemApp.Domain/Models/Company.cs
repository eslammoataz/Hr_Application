using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Domain.Models;

public class Company : AuditableEntity
{
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLogoUrl { get; set; }
    public int YearlyVacationDays { get; set; } = 21;
    public CompanyStatus Status { get; set; } = CompanyStatus.Active;

    // Navigation
    public ICollection<CompanyLocation> Locations { get; set; } = new List<CompanyLocation>();
    public ICollection<CompanyHierarchyPosition> HierarchyPositions { get; set; } = new List<CompanyHierarchyPosition>();
    public ICollection<Department> Departments { get; set; } = new List<Department>();
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}