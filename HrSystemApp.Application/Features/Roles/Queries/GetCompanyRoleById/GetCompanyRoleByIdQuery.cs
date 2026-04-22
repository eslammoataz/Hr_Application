using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Roles;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Queries.GetCompanyRoleById;

public sealed record GetCompanyRoleByIdQuery(Guid Id) : IRequest<Result<CompanyRoleDto>>;

public class GetCompanyRoleByIdQueryHandler : IRequestHandler<GetCompanyRoleByIdQuery, Result<CompanyRoleDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetCompanyRoleByIdQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<CompanyRoleDto>> Handle(
        GetCompanyRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<CompanyRoleDto>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee is null)
            return Result.Failure<CompanyRoleDto>(DomainErrors.Employee.NotFound);

        var role = await _unitOfWork.CompanyRoles.GetWithPermissionsAsync(request.Id, cancellationToken);
        if (role is null || role.CompanyId != employee.CompanyId)
            return Result.Failure<CompanyRoleDto>(DomainErrors.Roles.NotFound);

        return Result.Success(new CompanyRoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.Permissions.Select(p => p.Permission).ToList()));
    }
}
