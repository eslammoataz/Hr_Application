using Mapster;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Teams;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Teams.Queries.GetTeamById;

public class GetTeamByIdQueryHandler : IRequestHandler<GetTeamByIdQuery, Result<TeamWithMembersResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetTeamByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TeamWithMembersResponse>> Handle(GetTeamByIdQuery request, CancellationToken cancellationToken)
    {
        var team = await _unitOfWork.Teams.GetWithMembersAsync(request.Id, cancellationToken);
        if (team is null)
            return Result.Failure<TeamWithMembersResponse>(DomainErrors.Team.NotFound);

        return Result.Success(team.Adapt<TeamWithMembersResponse>());
    }
}
