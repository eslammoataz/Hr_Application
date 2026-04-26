using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.RequestTypes.Commands;

public record DeleteRequestTypeCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteRequestTypeCommandHandler : IRequestHandler<DeleteRequestTypeCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public DeleteRequestTypeCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(DeleteRequestTypeCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);

        var requestType = await _unitOfWork.RequestTypes.GetByIdAsync(request.Id, cancellationToken);
        if (requestType == null)
            return Result.Failure<bool>(DomainErrors.Requests.NotFound);

        // Cannot delete system types
        if (requestType.IsSystemType)
            return Result.Failure<bool>(DomainErrors.Requests.Locked);

        // Cannot delete types from other companies
        if (requestType.CompanyId != employee.CompanyId)
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);

        await _unitOfWork.RequestTypes.DeleteAsync(request.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
