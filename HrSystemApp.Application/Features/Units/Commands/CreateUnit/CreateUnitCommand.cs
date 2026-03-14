using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Units;
using MediatR;

namespace HrSystemApp.Application.Features.Units.Commands.CreateUnit;

public record CreateUnitCommand(
    Guid DepartmentId,
    string Name,
    string? Description,
    Guid? UnitLeaderId) : IRequest<Result<UnitResponse>>;
