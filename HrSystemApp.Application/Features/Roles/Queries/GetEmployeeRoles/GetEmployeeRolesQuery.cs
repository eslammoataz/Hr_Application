using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Roles;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Queries.GetEmployeeRoles;

public sealed record GetEmployeeRolesQuery(Guid EmployeeId) : IRequest<Result<IReadOnlyList<CompanyRoleSummaryDto>>>;

public class GetEmployeeRolesQueryHandler : IRequestHandler<GetEmployeeRolesQuery, Result<IReadOnlyList<CompanyRoleSummaryDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetEmployeeRolesQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<CompanyRoleSummaryDto>>> Handle(
        GetEmployeeRolesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<IReadOnlyList<CompanyRoleSummaryDto>>(DomainErrors.Auth.Unauthorized);

        var currentEmployee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (currentEmployee is null)
            return Result.Failure<IReadOnlyList<CompanyRoleSummaryDto>>(DomainErrors.Employee.NotFound);

        var targetEmployee = await _unitOfWork.Employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (targetEmployee is null || targetEmployee.CompanyId != currentEmployee.CompanyId)
            return Result.Failure<IReadOnlyList<CompanyRoleSummaryDto>>(DomainErrors.Employee.NotFound);

        var assignments = await _unitOfWork.EmployeeCompanyRoles.GetByEmployeeIdAsync(request.EmployeeId, cancellationToken);

        var dtos = assignments.Select(er => new CompanyRoleSummaryDto(
            er.Role.Id,
            er.Role.Name,
            er.Role.Description
        )).ToList();

        return Result.Success<IReadOnlyList<CompanyRoleSummaryDto>>(dtos);
    }
}
