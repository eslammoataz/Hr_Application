using HrSystemApp.Application.Common;
using MediatR;

namespace HrSystemApp.Application.Features.Departments.Commands.DeleteDepartment;

public record DeleteDepartmentCommand(Guid Id) : IRequest<Result>;
