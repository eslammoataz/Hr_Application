using HrSystemApp.Application.DTOs.Departments;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.Companies;

public record CreateCompanyRequest(
    string CompanyName,
    string? CompanyLogoUrl,
    int YearlyVacationDays);

public record UpdateCompanyRequest(
    string CompanyName,
    string? CompanyLogoUrl,
    int YearlyVacationDays,
    CompanyStatus Status);

public record ChangeCompanyStatusRequest(CompanyStatus Status);

public record CompanyResponse(
    Guid Id,
    string CompanyName,
    string? CompanyLogoUrl,
    int YearlyVacationDays,
    string Status,
    IReadOnlyList<CompanyLocationResponse> Locations,
    IReadOnlyList<DepartmentResponse> Departments);

public record CreateCompanyLocationRequest(
    string LocationName,
    string? Address,
    double? Latitude,
    double? Longitude);

public record CompanyLocationResponse(
    Guid Id,
    Guid CompanyId,
    string LocationName,
    string? Address,
    double? Latitude,
    double? Longitude);
