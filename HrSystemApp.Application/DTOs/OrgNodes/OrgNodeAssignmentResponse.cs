using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.OrgNodes;

public record OrgNodeAssignmentResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string? Email,
    OrgRole Role,
    string RoleName
);