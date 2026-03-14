using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.Employees;

public record CreateEmployeeRequest(
    string FullName,
    string Email,
    string PhoneNumber,
    Guid CompanyId,
    UserRole Role);

public record CreateEmployeeResponse(
    Guid EmployeeId,
    string UserId,
    string FullName,
    string Email,
    string PhoneNumber,
    string EmployeeCode,
    string Role,
    string TemporaryPassword);

public record EmployeeResponse(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber,
    string? Address,
    string EmployeeCode,
    Guid CompanyId,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? UnitId,
    string? UnitName,
    Guid? TeamId,
    string? TeamName,
    Guid? ManagerId,
    string? ManagerName,
    string EmploymentStatus,
    string Role,
    string? MedicalClass,
    DateTime CreatedAt);

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
