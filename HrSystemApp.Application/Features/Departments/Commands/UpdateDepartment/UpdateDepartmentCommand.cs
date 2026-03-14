using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Departments;
using MediatR;

namespace HrSystemApp.Application.Features.Departments.Commands.UpdateDepartment;

public record UpdateDepartmentCommand(
    Guid Id,
    string? Name,
    string? Description,
    Guid? VicePresidentId,
    Guid? ManagerId) : IRequest<Result<DepartmentResponse>>;
