using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Domain.Models;

public class Employee : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string Email { get; set; } = string.Empty;

    // Organization placement
    public Guid? DepartmentId { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? TeamId { get; set; }

    // Reporting
    public Guid? ManagerId { get; set; }

    // Employment info
    public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Active;
    public DateTime? ContractEndDate { get; set; }
    public Guid? InsurancePolicyId { get; set; }
    public MedicalClass? MedicalClass { get; set; }
    public Guid? SalaryPackageId { get; set; }
    public Guid? CompanyLocationId { get; set; }

    // Link to Identity
    public string? UserId { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
    public Department? Department { get; set; }
    public Unit? Unit { get; set; }
    public Team? Team { get; set; }
    public Employee? Manager { get; set; }
    public ApplicationUser? User { get; set; }
    public CompanyLocation? CompanyLocation { get; set; }

    // Reverse navigation

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public ICollection<AttendanceLog> AttendanceLogs { get; set; } = new List<AttendanceLog>();
}
