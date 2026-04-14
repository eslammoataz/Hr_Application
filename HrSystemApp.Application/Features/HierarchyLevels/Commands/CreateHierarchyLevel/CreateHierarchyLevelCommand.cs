using HrSystemApp.Application.Common;
using MediatR;

namespace HrSystemApp.Application.Features.HierarchyLevels.Commands.CreateHierarchyLevel;

public record CreateHierarchyLevelCommand : IRequest<Result<Guid>>
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public Guid? ParentLevelId { get; set; }
}