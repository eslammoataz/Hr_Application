using HrSystemApp.Application.Common;
using MediatR;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.UnassignEmployeeFromNode;

public record UnassignEmployeeFromNodeCommand(Guid OrgNodeId, Guid EmployeeId) : IRequest<Result<Guid>>;