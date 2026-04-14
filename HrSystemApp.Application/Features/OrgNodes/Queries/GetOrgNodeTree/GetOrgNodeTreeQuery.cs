using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.OrgNodes;
using MediatR;

namespace HrSystemApp.Application.Features.OrgNodes.Queries.GetOrgNodeTree;

public record GetOrgNodeTreeQuery : IRequest<Result<List<OrgNodeTreeResponse>>>
{
    public Guid? ParentId { get; set; }
    public int? Depth { get; set; }
}