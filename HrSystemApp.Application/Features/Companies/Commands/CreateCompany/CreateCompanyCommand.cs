using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;

namespace HrSystemApp.Application.Features.Companies.Commands.CreateCompany;

public record CreateCompanyCommand(
    string CompanyName,
    string? CompanyLogoUrl,
    int YearlyVacationDays,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int GraceMinutes,
    string TimeZoneId) : IRequest<Result<CompanyResponse>>;
