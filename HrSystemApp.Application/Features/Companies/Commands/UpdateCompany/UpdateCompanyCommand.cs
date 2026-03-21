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
    CompanyStatus Status) : IRequest<Result<CompanyResponse>>;
