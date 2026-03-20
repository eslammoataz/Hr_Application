using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Units;
using MediatR;

namespace HrSystemApp.Application.Features.Units.Queries.GetUnitById;

public record GetUnitByIdQuery(Guid Id) : IRequest<Result<UnitResponse>>;
