using HrSystemApp.Application.DTOs.Companies;

namespace HrSystemApp.Application.DTOs.Companies;

public record CompanyResponse(
    Guid Id,
    string CompanyName,
    string? CompanyLogoUrl,
    int YearlyVacationDays,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int GraceMinutes,
    string TimeZoneId,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<CompanyLocationResponse> Locations);
