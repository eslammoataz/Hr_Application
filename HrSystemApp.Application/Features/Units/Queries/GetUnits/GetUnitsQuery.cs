using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Units;
using MediatR;

namespace HrSystemApp.Application.Features.Units.Queries.GetUnits;

public record GetUnitsQuery(Guid DepartmentId) : IRequest<Result<IReadOnlyList<UnitResponse>>>;
