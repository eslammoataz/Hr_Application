using Mapster;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Units;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Units.Queries.GetUnits;

public class GetUnitsQueryHandler : IRequestHandler<GetUnitsQuery, Result<IReadOnlyList<UnitResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetUnitsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<UnitResponse>>> Handle(GetUnitsQuery request, CancellationToken cancellationToken)
    {
        var departmentExists = await _unitOfWork.Departments.ExistsAsync(d => d.Id == request.DepartmentId, cancellationToken);
        if (!departmentExists)
            return Result.Failure<IReadOnlyList<UnitResponse>>(DomainErrors.Department.NotFound);

        var units = await _unitOfWork.Units.GetByDepartmentAsync(request.DepartmentId, cancellationToken);
        return Result.Success(units.Adapt<IReadOnlyList<UnitResponse>>());
    }
}
