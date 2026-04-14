namespace HrSystemApp.Application.DTOs.OrgNodes;

public record OrgNodeChildResponse(
    Guid Id,
    string Name,
    string? Type
);
