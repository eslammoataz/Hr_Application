using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Companies.Queries.GetCompanyLocations;

public class
    GetCompanyLocationsQueryHandler : IRequestHandler<GetCompanyLocationsQuery,
    Result<IReadOnlyList<CompanyLocationResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetCompanyLocationsQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<CompanyLocationResponse>>> Handle(GetCompanyLocationsQuery request,
        CancellationToken cancellationToken)
    {
        Guid targetCompanyId;

        var currentRole = _currentUserService.Role;
        var isSuperAdmin = !string.IsNullOrEmpty(currentRole) &&
                           Enum.TryParse<UserRole>(currentRole, out var userRole) && userRole == UserRole.SuperAdmin;

        if (isSuperAdmin && request.CompanyId.HasValue)
        {
            targetCompanyId = request.CompanyId.Value;
        }
        else
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Result.Failure<IReadOnlyList<CompanyLocationResponse>>(DomainErrors.Auth.Unauthorized);

            var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
            if (employee == null)
                return Result.Failure<IReadOnlyList<CompanyLocationResponse>>(DomainErrors.Employee.NotFound);

            targetCompanyId = employee.CompanyId;
        }

        var company = await _unitOfWork.Companies.GetByIdAsync(targetCompanyId, cancellationToken);
        if (company == null)
        {
            return Result.Failure<IReadOnlyList<CompanyLocationResponse>>(DomainErrors.General.NotFound);
        }

        var locations = await _unitOfWork.CompanyLocations.FindAsync(
            l => l.CompanyId == targetCompanyId,
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
