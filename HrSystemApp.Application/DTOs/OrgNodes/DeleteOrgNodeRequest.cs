namespace HrSystemApp.Application.DTOs.OrgNodes;

public record DeleteOrgNodeRequest(
    Guid Id,
    string Mode = "reparent"
);