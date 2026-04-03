using HrSystemApp.Application.DTOs.Departments;

namespace HrSystemApp.Application.DTOs.Companies;

public record CompanyResponse(
    Guid Id,
    string CompanyName,
    string? CompanyLogoUrl,
    int YearlyVacationDays,
    string Status,
    IReadOnlyList<CompanyLocationResponse> Locations,
    IReadOnlyList<DepartmentResponse> Departments);
