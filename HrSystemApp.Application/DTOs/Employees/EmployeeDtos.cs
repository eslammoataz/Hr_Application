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
