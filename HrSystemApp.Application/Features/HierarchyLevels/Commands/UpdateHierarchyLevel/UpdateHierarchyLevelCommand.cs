using HrSystemApp.Application.Common;
using MediatR;

namespace HrSystemApp.Application.Features.HierarchyLevels.Commands.UpdateHierarchyLevel;

public record UpdateHierarchyLevelCommand : IRequest<Result<Guid>>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public Guid? ParentLevelId { get; set; }
}