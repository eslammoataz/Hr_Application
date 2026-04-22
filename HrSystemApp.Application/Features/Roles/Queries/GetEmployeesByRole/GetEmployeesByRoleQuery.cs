using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Roles;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Queries.GetEmployeesByRole;

public sealed record GetEmployeesByRoleQuery(Guid RoleId) : IRequest<Result<IReadOnlyList<EmployeeRoleSummaryDto>>>;

public class GetEmployeesByRoleQueryHandler : IRequestHandler<GetEmployeesByRoleQuery, Result<IReadOnlyList<EmployeeRoleSummaryDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetEmployeesByRoleQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<EmployeeRoleSummaryDto>>> Handle(
        GetEmployeesByRoleQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<IReadOnlyList<EmployeeRoleSummaryDto>>(DomainErrors.Auth.Unauthorized);

        var currentEmployee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (currentEmployee is null)
            return Result.Failure<IReadOnlyList<EmployeeRoleSummaryDto>>(DomainErrors.Employee.NotFound);

        var role = await _unitOfWork.CompanyRoles.GetByIdAsync(request.RoleId, cancellationToken);
        if (role is null || role.CompanyId != currentEmployee.CompanyId)
            return Result.Failure<IReadOnlyList<EmployeeRoleSummaryDto>>(DomainErrors.Roles.NotFound);

        var employees = await _unitOfWork.EmployeeCompanyRoles.GetActiveEmployeesByRoleIdAsync(request.RoleId, cancellationToken);

        var dtos = employees.Select(e => new EmployeeRoleSummaryDto(
            e.Id,
            e.FullName
        )).ToList();

        return Result.Success<IReadOnlyList<EmployeeRoleSummaryDto>>(dtos);
    }
}

public sealed record EmployeeRoleSummaryDto(Guid EmployeeId, string EmployeeName);
