namespace HrSystemApp.Application.Features.Employees.Commands.UpdateEmployee;

public record UpdateEmployeeRequest(
    string? FullName,
    string? PhoneNumber,
    string? Address,
    Guid? ManagerId,
    string? MedicalClass,
    DateTime? ContractEndDate);
