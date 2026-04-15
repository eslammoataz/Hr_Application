namespace HrSystemApp.Application.DTOs.Employees;

public record EmployeeResponse
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public Guid CompanyId { get; init; }
    public Guid? ManagerId { get; init; }
    public string? ManagerName { get; init; }
    public string EmploymentStatus { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? MedicalClass { get; init; }
    public DateTime CreatedAt { get; init; }
}
