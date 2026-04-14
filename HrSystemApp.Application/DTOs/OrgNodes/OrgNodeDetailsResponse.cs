using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.OrgNodes;

public record OrgNodeDetailsResponse(
    Guid Id,
    string Name,
    Guid? ParentId,
    string? ParentName,
    Guid? LevelId,
    string? LevelName,
    Guid? EntityId,
    OrgEntityType? EntityType,
    string? LinkedEntityName,
    List<OrgNodeAssignmentResponse> Assignments,
    List<OrgNodeChildResponse> Children
);