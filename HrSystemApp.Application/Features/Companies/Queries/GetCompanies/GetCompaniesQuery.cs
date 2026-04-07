using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.Companies.Queries.GetCompanies;

public record GetCompaniesQuery(
    string? SearchTerm,
    CompanyStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20,
    bool IncludeLocations = false,
    bool IncludeDepartments = false) : IRequest<Result<CompaniesPagedResult>>;
