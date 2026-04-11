using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Employees.Commands.CreateEmployee;

public record CreateEmployeeRequest(
    string FullName,
    string Email,
    string PhoneNumber,
    Guid CompanyId,
    UserRole Role,
    Guid? DepartmentId = null,
    Guid? UnitId = null,
    Guid? TeamId = null);
