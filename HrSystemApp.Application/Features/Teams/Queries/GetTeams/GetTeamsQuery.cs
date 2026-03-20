using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Teams;
using MediatR;

namespace HrSystemApp.Application.Features.Teams.Queries.GetTeams;

public record GetTeamsQuery(Guid UnitId) : IRequest<Result<IReadOnlyList<TeamResponse>>>;
