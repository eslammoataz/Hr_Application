using HrSystemApp.Application.Common;
using MediatR;

namespace HrSystemApp.Application.Features.Units.Commands.DeleteUnit;

public record DeleteUnitCommand(Guid Id) : IRequest<Result>;
