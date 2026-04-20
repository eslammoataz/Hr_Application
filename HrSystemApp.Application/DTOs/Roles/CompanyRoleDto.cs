namespace HrSystemApp.Application.DTOs.Roles;

public sealed record CompanyRoleSummaryDto(
    Guid Id,
    string Name,
    string? Description);

public sealed record CompanyRoleDto(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions);
