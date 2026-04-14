using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.OrgNodes;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.CreateOrgNode;

public record CreateOrgNodeCommand : IRequest<Result<Guid>>
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Guid? LevelId { get; set; }
    public Guid? EntityId { get; set; }
    public OrgEntityType? EntityType { get; set; }
}