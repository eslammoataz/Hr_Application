using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Commands.RemoveRoleFromEmployee;

public sealed record RemoveRoleFromEmployeeCommand(
    Guid EmployeeId,
    Guid RoleId) : IRequest<Result<bool>>;

public class RemoveRoleFromEmployeeCommandHandler : IRequestHandler<RemoveRoleFromEmployeeCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public RemoveRoleFromEmployeeCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(RemoveRoleFromEmployeeCommand request, CancellationToken cancellationToken)
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

        if (!await _unitOfWork.EmployeeCompanyRoles.ExistsAsync(request.EmployeeId, request.RoleId, cancellationToken))
            return Result.Failure<bool>(DomainErrors.Roles.AssignmentNotFound);

        await _unitOfWork.EmployeeCompanyRoles.RemoveAsync(request.EmployeeId, request.RoleId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
