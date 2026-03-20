namespace HrSystemApp.Application.DTOs.Companies;

public record CreateCompanyRequest(
    string CompanyName,
    string? CompanyLogoUrl,
    int YearlyVacationDays);

public record CompanyResponse(
    Guid Id,
    string CompanyName,
    string? CompanyLogoUrl,
    int YearlyVacationDays,
    string Status);

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
