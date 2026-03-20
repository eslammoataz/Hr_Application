using HrSystemApp.Application.Common;
using MediatR;

namespace HrSystemApp.Application.Features.Teams.Commands.DeleteTeam;

public record DeleteTeamCommand(Guid Id) : IRequest<Result>;
