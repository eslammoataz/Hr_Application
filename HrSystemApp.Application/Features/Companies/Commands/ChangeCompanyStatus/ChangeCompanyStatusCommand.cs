using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.Companies.Commands.ChangeCompanyStatus;

public record ChangeCompanyStatusCommand(
    Guid Id,
    CompanyStatus Status) : IRequest<Result<CompanyResponse>>;
