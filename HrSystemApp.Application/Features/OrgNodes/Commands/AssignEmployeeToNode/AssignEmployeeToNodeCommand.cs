using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.OrgNodes;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.AssignEmployeeToNode;

public record AssignEmployeeToNodeCommand : IRequest<Result<Guid>>
{
    public Guid OrgNodeId { get; set; }
    public Guid EmployeeId { get; set; }
    public OrgRole Role { get; set; }
}