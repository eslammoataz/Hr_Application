using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Teams;
using MediatR;

namespace HrSystemApp.Application.Features.Teams.Queries.GetTeamById;

public record GetTeamByIdQuery(Guid Id) : IRequest<Result<TeamWithMembersResponse>>;
