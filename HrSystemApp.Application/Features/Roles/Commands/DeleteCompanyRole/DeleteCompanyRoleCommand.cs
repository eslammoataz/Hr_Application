using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Commands.DeleteCompanyRole;

public sealed record DeleteCompanyRoleCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteCompanyRoleCommandHandler : IRequestHandler<DeleteCompanyRoleCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public DeleteCompanyRoleCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(DeleteCompanyRoleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee is null)
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);

        var role = await _unitOfWork.CompanyRoles.GetByIdAsync(request.Id, cancellationToken);
        if (role is null || role.CompanyId != employee.CompanyId)
            return Result.Failure<bool>(DomainErrors.Roles.NotFound);

        var isInUse = await _unitOfWork.RequestDefinitions.IsRoleInUseAsync(request.Id, cancellationToken);
        if (isInUse)
            return Result.Failure<bool>(DomainErrors.Roles.InUseByWorkflow);

        await _unitOfWork.CompanyRoles.DeleteAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
