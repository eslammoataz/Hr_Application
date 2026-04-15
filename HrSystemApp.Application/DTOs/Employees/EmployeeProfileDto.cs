namespace HrSystemApp.Application.DTOs.Employees;

public class EmployeeProfileDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string ManagerName { get; set; } = string.Empty;
    public string EmploymentStatus { get; set; } = string.Empty;
    public string? MedicalClass { get; set; }
    public string CompanyLocationName { get; set; } = string.Empty;
    public DateTime? ContractEndDate { get; set; }
}
