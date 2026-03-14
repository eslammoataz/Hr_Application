using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;

namespace HrSystemApp.Application.Features.Companies.Commands.CreateCompany;

public record CreateCompanyCommand(
    string CompanyName,
    string? CompanyLogoUrl,
    int YearlyVacationDays) : IRequest<Result<CompanyResponse>>;
