using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Companies.Commands.DeleteCompanyLocation;

public class DeleteCompanyLocationCommandHandler : IRequestHandler<DeleteCompanyLocationCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public DeleteCompanyLocationCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Guid>> Handle(DeleteCompanyLocationCommand request, CancellationToken cancellationToken)
    {
        var location = await _unitOfWork.CompanyLocations.GetByIdAsync(request.LocationId, cancellationToken);
        if (location == null)
        {
            return Result.Failure<Guid>(DomainErrors.General.NotFound);
        }

        if (_currentUserService.Role != nameof(UserRole.SuperAdmin))
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);

            var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
            if (employee == null)
                return Result.Failure<Guid>(DomainErrors.Employee.NotFound);

            if (employee.CompanyId != location.CompanyId)
                return Result.Failure<Guid>(DomainErrors.General.Forbidden);
        }

        await _unitOfWork.CompanyLocations.DeleteAsync(location, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(location.Id);
    }
}
