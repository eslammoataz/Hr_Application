using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Teams;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.Teams.Commands.CreateTeam;

public class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, Result<TeamResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateTeamCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<Result<TeamResponse>> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var unit = await _unitOfWork.Units.GetByIdAsync(request.UnitId, cancellationToken);
        if (unit is null)
            return Result.Failure<TeamResponse>(DomainErrors.Unit.NotFound);

        var nameExists = await _unitOfWork.Teams.ExistsAsync(
            t => t.UnitId == request.UnitId && t.Name == request.Name, cancellationToken);
        if (nameExists)
            return Result.Failure<TeamResponse>(DomainErrors.Team.AlreadyExists);

        var team = new Team
        {
            UnitId = request.UnitId,
            Name = request.Name,
            Description = request.Description,
            TeamLeaderId = request.TeamLeaderId
        };

        await _unitOfWork.Teams.AddAsync(team, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new TeamResponse(
            team.Id, team.UnitId, unit.Name, team.Name,
            team.Description, team.TeamLeaderId, null, team.CreatedAt));
    }
}
