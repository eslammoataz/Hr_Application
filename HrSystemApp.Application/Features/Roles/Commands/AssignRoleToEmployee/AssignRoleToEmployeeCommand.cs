using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Commands.AssignRoleToEmployee;

public sealed record AssignRoleToEmployeeCommand(
    Guid EmployeeId,
    Guid RoleId) : IRequest<Result<bool>>;

public class AssignRoleToEmployeeCommandHandler : IRequestHandler<AssignRoleToEmployeeCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AssignRoleToEmployeeCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(AssignRoleToEmployeeCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);

        var currentEmployee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (currentEmployee is null)
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);

        var targetEmployee = await _unitOfWork.Employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (targetEmployee is null || targetEmployee.CompanyId != currentEmployee.CompanyId)
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);

        var role = await _unitOfWork.CompanyRoles.GetByIdAsync(request.RoleId, cancellationToken);
        if (role is null || role.CompanyId != currentEmployee.CompanyId)
            return Result.Failure<bool>(DomainErrors.Roles.NotFound);

        if (await _unitOfWork.EmployeeCompanyRoles.ExistsAsync(request.EmployeeId, request.RoleId, cancellationToken))
            return Result.Failure<bool>(DomainErrors.Roles.AlreadyAssigned);

        var assignment = new EmployeeCompanyRole
        {
            EmployeeId = request.EmployeeId,
            RoleId = request.RoleId,
            AssignedAtUtc = DateTime.UtcNow
        };

        await _unitOfWork.EmployeeCompanyRoles.AddAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
