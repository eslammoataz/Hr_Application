using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.OrgNodes;

public record CreateOrgNodeRequest(
    string Name,
    Guid? ParentId,
    Guid? LevelId,
    Guid? EntityId,
    OrgEntityType? EntityType
);