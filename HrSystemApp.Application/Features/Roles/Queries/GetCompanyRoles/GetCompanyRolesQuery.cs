using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Roles;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Queries.GetCompanyRoles;

public sealed record GetCompanyRolesQuery : IRequest<Result<IReadOnlyList<CompanyRoleDto>>>;

public class GetCompanyRolesQueryHandler : IRequestHandler<GetCompanyRolesQuery, Result<IReadOnlyList<CompanyRoleDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetCompanyRolesQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<CompanyRoleDto>>> Handle(
        GetCompanyRolesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<IReadOnlyList<CompanyRoleDto>>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee is null)
            return Result.Failure<IReadOnlyList<CompanyRoleDto>>(DomainErrors.Employee.NotFound);

        var roles = await _unitOfWork.CompanyRoles.GetByCompanyAsync(employee.CompanyId, cancellationToken);

        var dtos = roles.Select(r => new CompanyRoleDto(
            r.Id,
            r.Name,
            r.Description,
            r.Permissions.Select(p => p.Permission).ToList()
        )).ToList();

        return Result.Success<IReadOnlyList<CompanyRoleDto>>(dtos);
    }
}
