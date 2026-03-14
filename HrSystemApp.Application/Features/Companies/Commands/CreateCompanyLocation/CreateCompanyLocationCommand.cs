using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;

namespace HrSystemApp.Application.Features.Companies.Commands.CreateCompanyLocation;

public record CreateCompanyLocationCommand(
    Guid CompanyId,
    string LocationName,
    string? Address,
    double? Latitude,
    double? Longitude) : IRequest<Result<CompanyLocationResponse>>;
