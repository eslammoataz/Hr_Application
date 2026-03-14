using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Teams;
using MediatR;

namespace HrSystemApp.Application.Features.Teams.Commands.CreateTeam;

public record CreateTeamCommand(
    Guid UnitId,
    string Name,
    string? Description,
    Guid? TeamLeaderId) : IRequest<Result<TeamResponse>>;
