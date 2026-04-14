namespace HrSystemApp.Application.DTOs.HierarchyLevels;

public record UpdateHierarchyLevelRequest(
    Guid Id,
    string Name,
    int SortOrder,
    Guid? ParentLevelId
);