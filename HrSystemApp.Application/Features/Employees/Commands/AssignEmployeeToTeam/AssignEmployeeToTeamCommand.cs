using HrSystemApp.Application.Common;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Commands.AssignEmployeeToTeam;

public record AssignEmployeeToTeamCommand(Guid EmployeeId, Guid TeamId) : IRequest<Result>;
