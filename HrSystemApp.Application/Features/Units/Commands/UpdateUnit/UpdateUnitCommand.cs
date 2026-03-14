using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Units;
using MediatR;

namespace HrSystemApp.Application.Features.Units.Commands.UpdateUnit;

public record UpdateUnitCommand(
    Guid Id,
    string? Name,
    string? Description,
    Guid? UnitLeaderId) : IRequest<Result<UnitResponse>>;
