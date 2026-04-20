namespace HrSystemApp.Application.DTOs.OrgNodes;

public record OrgNodeDetailsResponse(
    Guid Id,
    string Name,
    Guid? ParentId,
    string? ParentName,
    string? Type,
    List<OrgNodeAssignmentResponse> Assignments,
    List<OrgNodeChildResponse> Children);
