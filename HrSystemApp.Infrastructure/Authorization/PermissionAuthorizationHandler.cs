using HrSystemApp.Application.Authorization;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;

namespace HrSystemApp.Infrastructure.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public PermissionAuthorizationHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return;

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, default);
        if (employee is null)
            return;

        var permissions = await _unitOfWork.EmployeeCompanyRoles
            .GetPermissionsForEmployeeAsync(employee.Id, default);

        if (permissions.Contains(requirement.Permission))
            context.Succeed(requirement);
    }
}
