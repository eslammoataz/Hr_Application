using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.OrgNodes;

public record OrgNodeTreeResponse(
    Guid Id,
    string Name,
    Guid? LevelId,
    string? LevelName,
    bool HasChildren,
    List<OrgNodeTreeResponse> Children,
    Guid? EntityId,
    OrgEntityType? EntityType,
    List<OrgNodeAssignmentResponse> Assignments);
