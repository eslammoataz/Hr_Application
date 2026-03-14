using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Teams;
using MediatR;

namespace HrSystemApp.Application.Features.Teams.Commands.UpdateTeam;

public record UpdateTeamCommand(
    Guid Id,
    string? Name,
    string? Description,
    Guid? TeamLeaderId) : IRequest<Result<TeamResponse>>;
