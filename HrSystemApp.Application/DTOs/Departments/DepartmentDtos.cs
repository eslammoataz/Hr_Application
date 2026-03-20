namespace HrSystemApp.Application.DTOs.Departments;

public record CreateDepartmentRequest(
    Guid CompanyId,
    string Name,
    string? Description,
    Guid? VicePresidentId,
    Guid? ManagerId);

public record UpdateDepartmentRequest(
    string? Name,
    string? Description,
    Guid? VicePresidentId,
    Guid? ManagerId);

public record DepartmentResponse(
    Guid Id,
    Guid CompanyId,
    string Name,
    string? Description,
    Guid? VicePresidentId,
    string? VicePresidentName,
    Guid? ManagerId,
    string? ManagerName,
    DateTime CreatedAt);

public record DepartmentWithUnitsResponse(
    Guid Id,
    Guid CompanyId,
    string Name,
    string? Description,
    Guid? VicePresidentId,
    string? VicePresidentName,
    Guid? ManagerId,
    string? ManagerName,
    IReadOnlyList<UnitSummary> Units,
    DateTime CreatedAt);

public record UnitSummary(Guid Id, string Name);
