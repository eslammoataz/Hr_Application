using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.Employees;

public record CreateEmployeeRequest(
    string FullName,
    string Email,
    string PhoneNumber,
    Guid CompanyId,
    UserRole Role);

public record CreateEmployeeResponse
{
    public Guid EmployeeId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string EmployeeCode { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string TemporaryPassword { get; init; } = string.Empty;
}

public record EmployeeResponse
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public Guid CompanyId { get; init; }
    public Guid? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public Guid? UnitId { get; init; }
    public string? UnitName { get; init; }
    public Guid? TeamId { get; init; }
    public string? TeamName { get; init; }
    public Guid? ManagerId { get; init; }
    public string? ManagerName { get; init; }
    public string EmploymentStatus { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? MedicalClass { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record UpdateEmployeeRequest(
    string? FullName,
    string? PhoneNumber,
    string? Address,
    Guid? DepartmentId,
    Guid? UnitId,
    Guid? TeamId,
    Guid? ManagerId,
    string? MedicalClass,
    DateTime? ContractEndDate);

public record AssignEmployeeToTeamRequest(Guid TeamId);

public record UpdateEmployeeProfileRequest(
    string? PhoneNumber,
    string? Address);
