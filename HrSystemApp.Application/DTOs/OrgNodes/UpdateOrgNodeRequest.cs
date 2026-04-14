namespace HrSystemApp.Application.DTOs.OrgNodes;

public record UpdateOrgNodeRequest(
    Guid Id,
    string Name,
    Guid? ParentId,
    Guid? LevelId
);