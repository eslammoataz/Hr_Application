using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Constants;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Commands.CreateCompanyRole;

public sealed record CreateCompanyRoleCommand(
    string Name,
    string? Description,
    IReadOnlyList<string>? Permissions) : IRequest<Result<Guid>>;

public class CreateCompanyRoleCommandHandler : IRequestHandler<CreateCompanyRoleCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public CreateCompanyRoleCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Guid>> Handle(CreateCompanyRoleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee is null)
            return Result.Failure<Guid>(DomainErrors.Employee.NotFound);

        if (await _unitOfWork.CompanyRoles.ExistsByNameAsync(employee.CompanyId, request.Name, null, cancellationToken))
            return Result.Failure<Guid>(DomainErrors.Roles.NameAlreadyExists);

        var permissions = request.Permissions?
            .Distinct()
            .Where(p => AppPermissions.All.Contains(p))
            .Select(p => new CompanyRolePermission { Permission = p })
            .ToList() ?? new List<CompanyRolePermission>();

        var role = new CompanyRole
        {
            CompanyId = employee.CompanyId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Permissions = permissions
        };

        await _unitOfWork.CompanyRoles.AddAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(role.Id);
    }
}
