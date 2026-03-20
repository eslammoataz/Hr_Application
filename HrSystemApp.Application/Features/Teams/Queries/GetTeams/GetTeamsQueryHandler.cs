using AutoMapper;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Teams;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Teams.Queries.GetTeams;

public class GetTeamsQueryHandler : IRequestHandler<GetTeamsQuery, Result<IReadOnlyList<TeamResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetTeamsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<TeamResponse>>> Handle(GetTeamsQuery request, CancellationToken cancellationToken)
    {
        var unitExists = await _unitOfWork.Units.ExistsAsync(u => u.Id == request.UnitId, cancellationToken);
        if (!unitExists)
            return Result.Failure<IReadOnlyList<TeamResponse>>(DomainErrors.Unit.NotFound);

        var teams = await _unitOfWork.Teams.GetByUnitAsync(request.UnitId, cancellationToken);
        return Result.Success(_mapper.Map<IReadOnlyList<TeamResponse>>(teams));
    }
}
