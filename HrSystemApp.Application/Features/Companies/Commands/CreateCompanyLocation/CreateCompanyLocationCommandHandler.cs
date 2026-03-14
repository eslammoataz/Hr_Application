using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Models;
using HrSystemApp.Application.Errors;

namespace HrSystemApp.Application.Features.Companies.Commands.CreateCompanyLocation;

public class CreateCompanyLocationCommandHandler : IRequestHandler<CreateCompanyLocationCommand, Result<CompanyLocationResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateCompanyLocationCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CompanyLocationResponse>> Handle(CreateCompanyLocationCommand request, CancellationToken cancellationToken)
    {
        var company = await _unitOfWork.Companies.GetByIdAsync(request.CompanyId, cancellationToken);
        if (company == null)
        {
            return Result.Failure<CompanyLocationResponse>(DomainErrors.General.NotFound);
        }

        var location = new CompanyLocation
        {
            CompanyId = request.CompanyId,
            LocationName = request.LocationName,
            Address = request.Address,
            Latitude = request.Latitude,
            Longitude = request.Longitude
        };

        await _unitOfWork.CompanyLocations.AddAsync(location, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CompanyLocationResponse(
            location.Id,
            location.CompanyId,
            location.LocationName,
            location.Address,
            location.Latitude,
            location.Longitude
        ));
    }
}
