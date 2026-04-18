using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using MediatR;

namespace HrSystemApp.Application.Features.Companies.Queries.GetMyCompany;

public record GetMyCompanyQuery(
    bool IncludeLocations = false) : IRequest<Result<CompanyResponse>>;
