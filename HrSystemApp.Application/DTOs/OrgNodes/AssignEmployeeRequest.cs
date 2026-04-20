using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.OrgNodes;

public record AssignEmployeeRequest(
    Guid OrgNodeId,
    Guid EmployeeId,
    OrgRole Role);
