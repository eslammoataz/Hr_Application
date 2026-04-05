using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Features.Companies.Commands.CreateCompanyLocation;

public class CreateCompanyLocationCommandHandler : IRequestHandler<CreateCompanyLocationCommand, Result<CompanyLocationResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public CreateCompanyLocationCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<CompanyLocationResponse>> Handle(CreateCompanyLocationCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != nameof(UserRole.SuperAdmin))
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Result.Failure<CompanyLocationResponse>(DomainErrors.Auth.Unauthorized);

            var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
            if (employee == null)
                return Result.Failure<CompanyLocationResponse>(DomainErrors.Employee.NotFound);

            if (request.CompanyId != employee.CompanyId)
                return Result.Failure<CompanyLocationResponse>(DomainErrors.General.Forbidden);
        }

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
