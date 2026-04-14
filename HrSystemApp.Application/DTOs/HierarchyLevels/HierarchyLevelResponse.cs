namespace HrSystemApp.Application.DTOs.HierarchyLevels;

public record HierarchyLevelResponse(
    Guid Id,
    string Name,
    int SortOrder,
    Guid? ParentLevelId,
    string? ParentLevelName,
    int NodeCount
);