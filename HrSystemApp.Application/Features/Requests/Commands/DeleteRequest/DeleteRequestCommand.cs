using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.Requests.Commands.DeleteRequest;

public record DeleteRequestCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteRequestCommandHandler : IRequestHandler<DeleteRequestCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DeleteRequestCommandHandler> _logger;

    public DeleteRequestCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, ILogger<DeleteRequestCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(DeleteRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<bool>(new Error("Auth.Unauthorized", "User not authenticated."));

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogWarning("DeleteRequest failed: Employee profile not found for UserId {UserId}", userId);
            return Result.Failure<bool>(new Error("Employee.NotFound", "Employee profile not found."));
        }

        var existingRequest = await _unitOfWork.Requests.GetByIdWithHistoryAsync(request.Id, cancellationToken);
        if (existingRequest == null)
            return Result.Failure<bool>(new Error("Request.NotFound", "Request not found."));

        // 1. Security: Only the owner can delete
        if (existingRequest.EmployeeId != employee.Id)
            return Result.Failure<bool>(new Error("Request.Unauthorized", "You can only delete your own requests."));

        // 2. Status: Approved requests cannot be deleted
        if (existingRequest.Status == RequestStatus.Approved)
        {
            _logger.LogWarning("DeleteRequest failed: Request {RequestId} is already approved and locked.", request.Id);
            return Result.Failure<bool>(new Error("Request.Locked", "Approved requests cannot be deleted."));
        }

        // 3. Chain check
        if (existingRequest.ApprovalHistory.Any())
        {
            _logger.LogWarning("DeleteRequest failed: Request {RequestId} has history and cannot be deleted.", request.Id);
            return Result.Failure<bool>(new Error("Request.Locked", "Request cannot be deleted because the approval process has already started."));
        }

        _logger.LogInformation("Employee {EmployeeId} is deleting request {RequestId} of type {Type}", 
            employee.Id, existingRequest.Id, existingRequest.RequestType);

        await _unitOfWork.Requests.DeleteAsync(existingRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
