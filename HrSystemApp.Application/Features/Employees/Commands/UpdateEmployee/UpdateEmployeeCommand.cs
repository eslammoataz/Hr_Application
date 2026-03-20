using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Commands.UpdateEmployee;

public record UpdateEmployeeCommand(
    Guid Id,
    string? FullName,
    string? PhoneNumber,
    string? Address,
    Guid? DepartmentId,
    Guid? UnitId,
    Guid? TeamId,
    Guid? ManagerId,
    string? MedicalClass,
    DateTime? ContractEndDate) : IRequest<Result<EmployeeResponse>>;
