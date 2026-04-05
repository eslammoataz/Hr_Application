using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using MediatR;

namespace HrSystemApp.Application.Features.Companies.Queries.GetMyCompany;

public record GetMyCompanyQuery(
    bool IncludeLocations = false,
    bool IncludeDepartments = false) : IRequest<Result<CompanyResponse>>;
