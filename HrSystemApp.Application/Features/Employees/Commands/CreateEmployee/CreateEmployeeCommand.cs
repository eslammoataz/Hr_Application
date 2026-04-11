using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Employees.Commands.CreateEmployee;

public record CreateEmployeeCommand(
    string FullName,
    string Email,
    string PhoneNumber,
    Guid CompanyId,
    UserRole Role,
    Guid? DepartmentId = null,
    Guid? UnitId = null,
    Guid? TeamId = null) : IRequest<Result<CreateEmployeeResponse>>;
