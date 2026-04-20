namespace HrSystemApp.Application.DTOs.OrgNodes;

public record CreateOrgNodeRequest(
    string Name,
    Guid? ParentId,
    string? Type);
