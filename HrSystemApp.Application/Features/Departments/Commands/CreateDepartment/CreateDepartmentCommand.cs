using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Departments;
using MediatR;

namespace HrSystemApp.Application.Features.Departments.Commands.CreateDepartment;

public record CreateDepartmentCommand(
    Guid CompanyId,
    string Name,
    string? Description,
    Guid? VicePresidentId,
    Guid? ManagerId) : IRequest<Result<DepartmentResponse>>;
