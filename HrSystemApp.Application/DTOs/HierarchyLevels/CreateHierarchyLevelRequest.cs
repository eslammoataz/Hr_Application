namespace HrSystemApp.Application.DTOs.HierarchyLevels;

public record CreateHierarchyLevelRequest(
    string Name,
    int SortOrder,
    Guid? ParentLevelId
);