using HrSystemApp.Application.Common;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Commands.ChangeEmployeeStatus;

public record ChangeEmployeeStatusCommand(Guid Id, int Status) : IRequest<Result>;
