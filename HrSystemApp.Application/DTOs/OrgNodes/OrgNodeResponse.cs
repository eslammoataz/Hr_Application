using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.OrgNodes;

public record OrgNodeResponse(
    Guid Id,
    string Name,
    Guid? ParentId,
    Guid? LevelId,
    string? LevelName,
    Guid? EntityId,
    OrgEntityType? EntityType,
    bool HasChildren,
    int AssignmentCount
);