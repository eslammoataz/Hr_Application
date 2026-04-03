using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.Requests.Commands.RejectRequest;

public record RejectRequestCommand(Guid RequestId, string Reason) : IRequest<Result<bool>>;

public class RejectRequestCommandHandler : IRequestHandler<RejectRequestCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<RejectRequestCommandHandler> _logger;

    public RejectRequestCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        INotificationService notificationService,
        ILogger<RejectRequestCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(RejectRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);

        var existingRequest = await _unitOfWork.Requests.GetByIdWithHistoryAsync(request.RequestId, cancellationToken);
        if (existingRequest == null)
            return Result.Failure<bool>(DomainErrors.Requests.NotFound);

        // 1. Security: Is this the current approver?
        if (existingRequest.CurrentApproverId != employee.Id)
            return Result.Failure<bool>(DomainErrors.Requests.Unauthorized);

        // 2. Action: Set to Rejected
        existingRequest.Status = RequestStatus.Rejected;
        existingRequest.CurrentApproverId = null; // Process stops

        var history = new RequestApprovalHistory
        {
            RequestId = existingRequest.Id,
            ApproverId = employee.Id,
            Status = RequestStatus.Rejected,
            Comment = request.Reason
        };
        existingRequest.ApprovalHistory.Add(history);

        await _unitOfWork.Requests.UpdateAsync(existingRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await _notificationService.SendNotificationAsync(
                existingRequest.EmployeeId,
                "Request Rejected",
                $"Your {existingRequest.RequestType} request has been rejected. Reason: {request.Reason}",
                NotificationType.RequestRejected);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Request {RequestId} rejected, but notification delivery failed.", existingRequest.Id);
        }

        return Result.Success(true);
    }
}
