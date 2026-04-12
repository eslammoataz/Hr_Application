using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.Companies.Commands.UpdateCompany;

public record UpdateCompanyCommand(
    Guid Id,
    string CompanyName,
    string? CompanyLogoUrl,
    int YearlyVacationDays,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int GraceMinutes,
    string? TimeZoneId) : IRequest<Result<CompanyResponse>>;
