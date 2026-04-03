namespace HrSystemApp.Application.Features.Employees.Commands.UpdateEmployee;

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
