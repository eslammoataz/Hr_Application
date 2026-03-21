using Mapster;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Teams;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Teams.Commands.UpdateTeam;

public class UpdateTeamCommandHandler : IRequestHandler<UpdateTeamCommand, Result<TeamResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTeamCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TeamResponse>> Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _unitOfWork.Teams.GetByIdAsync(request.Id, cancellationToken);
        if (team is null)
            return Result.Failure<TeamResponse>(DomainErrors.Team.NotFound);

        if (request.Name is not null && request.Name != team.Name)
        {
            var nameExists = await _unitOfWork.Teams.ExistsAsync(
                t => t.UnitId == team.UnitId && t.Name == request.Name && t.Id != request.Id,
                cancellationToken);
            if (nameExists)
                return Result.Failure<TeamResponse>(DomainErrors.Team.AlreadyExists);
        }

        request.Adapt(team);

        await _unitOfWork.Teams.UpdateAsync(team, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(team.Adapt<TeamResponse>());
    }
}
