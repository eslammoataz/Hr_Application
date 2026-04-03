using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Companies.Commands.UpdateCompany;

public record UpdateCompanyRequest(
    string CompanyName,
    string? CompanyLogoUrl,
    int YearlyVacationDays,
    CompanyStatus Status);
