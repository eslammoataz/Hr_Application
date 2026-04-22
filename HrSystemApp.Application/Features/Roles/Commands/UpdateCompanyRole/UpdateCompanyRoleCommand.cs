using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Constants;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Commands.UpdateCompanyRole;

public sealed record UpdateCompanyRoleCommand(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<string>? Permissions) : IRequest<Result<bool>>;

public class UpdateCompanyRoleCommandHandler : IRequestHandler<UpdateCompanyRoleCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateCompanyRoleCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(UpdateCompanyRoleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee is null)
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);

        var role = await _unitOfWork.CompanyRoles.GetWithPermissionsAsync(request.Id, cancellationToken);
        if (role is null || role.CompanyId != employee.CompanyId)
            return Result.Failure<bool>(DomainErrors.Roles.NotFound);

        if (await _unitOfWork.CompanyRoles.ExistsByNameAsync(employee.CompanyId, request.Name, request.Id, cancellationToken))
            return Result.Failure<bool>(DomainErrors.Roles.NameAlreadyExists);

        role.Name = request.Name.Trim();
        role.Description = request.Description?.Trim();

        if (request.Permissions is not null)
        {
            var invalidPerms = request.Permissions
                .Where(p => !AppPermissions.All.Contains(p))
                .ToList();
            if (invalidPerms.Any())
                return Result.Failure<bool>(DomainErrors.Roles.InvalidPermission);

            await _unitOfWork.CompanyRoles.ReplacePermissionsAsync(role.Id, request.Permissions, cancellationToken);
        }

        await _unitOfWork.CompanyRoles.UpdateAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
