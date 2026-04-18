namespace HrSystemApp.Application.DTOs.OrgNodes;

public record OrgNodeTreeResponse(
    Guid Id,
    string Name,
    bool HasChildren,
    List<OrgNodeTreeResponse> Children,
    string? Type,
    List<OrgNodeAssignmentResponse> Assignments);
