using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;

namespace HrSystemApp.Application.Features.Companies.Queries.GetCompanyLocations;

public class
    GetCompanyLocationsQueryHandler : IRequestHandler<GetCompanyLocationsQuery,
    Result<IReadOnlyList<CompanyLocationResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetCompanyLocationsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<CompanyLocationResponse>>> Handle(GetCompanyLocationsQuery request,
        CancellationToken cancellationToken)
    {
        var company = await _unitOfWork.Companies.GetByIdAsync(request.CompanyId, cancellationToken);
        if (company == null)
        {
            return Result.Failure<IReadOnlyList<CompanyLocationResponse>>(DomainErrors.General.NotFound);
        }

        var locations = await _unitOfWork.CompanyLocations.FindAsync(
            l => l.CompanyId == request.CompanyId,
            cancellationToken);

        var response = locations.Select(l => new CompanyLocationResponse(
            l.Id,
            l.CompanyId,
            l.LocationName,
            l.Address,
            l.Latitude,
            l.Longitude
        )).ToList();

        return Result.Success((IReadOnlyList<CompanyLocationResponse>)response);
    }
}
