using HrSystemApp.Application.Common;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Commands.DeactivateEmployee;

public record DeactivateEmployeeCommand(Guid Id) : IRequest<Result>;
