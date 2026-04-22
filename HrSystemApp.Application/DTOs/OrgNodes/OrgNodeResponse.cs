namespace HrSystemApp.Application.DTOs.OrgNodes;

public record OrgNodeResponse(
    Guid Id,
    string Name,
    Guid? ParentId,
    string? Type,
    bool HasChildren,
    int AssignmentCount);
