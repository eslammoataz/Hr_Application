using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;

namespace HrSystemApp.Application.Features.Companies.Queries.GetCompanyLocations;

public record GetCompanyLocationsQuery(Guid CompanyId) : IRequest<Result<IReadOnlyList<CompanyLocationResponse>>>;
