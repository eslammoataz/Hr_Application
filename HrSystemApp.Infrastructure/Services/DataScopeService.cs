using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Infrastructure.Services;

public class DataScopeService : IDataScopeService
{
    private readonly ICurrentUserService _currentUserService;

    public DataScopeService(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public Result<Guid?> ResolveEmployeeCompanyScope(Guid? requestedCompanyId)
    {
        if (!_currentUserService.IsAuthenticated)
        {
            return Result.Failure<Guid?>(DomainErrors.Auth.Unauthorized);
        }

        if (!Enum.TryParse<UserRole>(_currentUserService.Role, out var role))
        {
            return Result.Failure<Guid?>(DomainErrors.General.Forbidden);
        }

        if (role == UserRole.SuperAdmin)
        {
            return Result.Success(requestedCompanyId);
        }

        if (!_currentUserService.CompanyId.HasValue)
        {
            return Result.Failure<Guid?>(DomainErrors.Auth.Unauthorized);
        }

        return Result.Success((Guid?)_currentUserService.CompanyId.Value);
    }
}
