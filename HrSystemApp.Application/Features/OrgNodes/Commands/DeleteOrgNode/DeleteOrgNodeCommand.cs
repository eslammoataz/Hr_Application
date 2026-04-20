using HrSystemApp.Application.Common;
using MediatR;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.DeleteOrgNode;

public record DeleteOrgNodeCommand(Guid Id) : IRequest<Result<Guid>>;
