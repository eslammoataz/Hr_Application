using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using MediatR;

namespace HrSystemApp.Application.Features.Companies.Queries.GetCompanyById;

public record GetCompanyByIdQuery(
    Guid Id,
    bool IncludeLocations = false) : IRequest<Result<CompanyResponse>>;
