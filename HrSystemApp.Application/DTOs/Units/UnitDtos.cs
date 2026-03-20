namespace HrSystemApp.Application.DTOs.Units;

public record CreateUnitRequest(
    Guid DepartmentId,
    string Name,
    string? Description,
    Guid? UnitLeaderId);

public record UpdateUnitRequest(
    string? Name,
    string? Description,
    Guid? UnitLeaderId);

public record UnitResponse(
    Guid Id,
    Guid DepartmentId,
    string DepartmentName,
    string Name,
    string? Description,
    Guid? UnitLeaderId,
    string? UnitLeaderName,
    DateTime CreatedAt);
