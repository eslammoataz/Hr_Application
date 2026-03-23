using Mapster;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Units;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Units.Queries.GetUnitById;

public class GetUnitByIdQueryHandler : IRequestHandler<GetUnitByIdQuery, Result<UnitResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetUnitByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UnitResponse>> Handle(GetUnitByIdQuery request, CancellationToken cancellationToken)
    {
        var unit = await _unitOfWork.Units.GetByIdAsync(request.Id, cancellationToken);
        if (unit is null)
            return Result.Failure<UnitResponse>(DomainErrors.Unit.NotFound);

        return Result.Success(unit.Adapt<UnitResponse>());
    }
}
