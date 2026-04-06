namespace HrSystemApp.Application.Features.Companies.Commands.UpdateCompany;

public record UpdateCompanyRequest(
    string CompanyName,
    string? CompanyLogoUrl,
    int YearlyVacationDays,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int GraceMinutes,
    string TimeZoneId);
