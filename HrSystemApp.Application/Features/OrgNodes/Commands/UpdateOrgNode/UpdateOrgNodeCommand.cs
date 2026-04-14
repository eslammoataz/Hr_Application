using HrSystemApp.Application.Common;
using MediatR;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.UpdateOrgNode;

public record UpdateOrgNodeCommand : IRequest<Result<Guid>>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Guid? LevelId { get; set; }
}