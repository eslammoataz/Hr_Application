using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
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

        // 1. Status: Is it pending?
        if (existingRequest.Status != RequestStatus.Submitted && existingRequest.Status != RequestStatus.InProgress)
            return Result.Failure<bool>(DomainErrors.Requests.Locked);

        // 2. Deserialize planned steps
        var plannedSteps = JsonSerializer.Deserialize<List<PlannedStepDto>>(existingRequest.PlannedStepsJson ?? "[]");
        if (plannedSteps == null || plannedSteps.Count == 0)
        {
            _logger.LogWarning("RejectRequest failed: No planned steps found for request {RequestId}", request.RequestId);
            return Result.Failure<bool>(DomainErrors.Request.InvalidWorkflowChain);
        }

        // 3. Validate current step order
        if (existingRequest.CurrentStepOrder < 1 || existingRequest.CurrentStepOrder > plannedSteps.Count)
        {
            _logger.LogWarning("RejectRequest failed: Invalid step order {StepOrder} for request {RequestId}",
                existingRequest.CurrentStepOrder, request.RequestId);
            return Result.Failure<bool>(DomainErrors.Request.StepOrderExceeded);
        }

        // 4. Get current step and validate approver
        var currentStep = plannedSteps[existingRequest.CurrentStepOrder - 1];
        var approverIds = currentStep.Approvers.Select(a => a.EmployeeId).ToList();

        if (!approverIds.Contains(employee.Id))
        {
            _logger.LogWarning("Unauthorized reject attempt for request {RequestId} by {EmployeeId}.",
                request.RequestId, employee.Id);
            return Result.Failure<bool>(DomainErrors.Requests.Unauthorized);
        }

        // 5. Reject the request
        existingRequest.Status = RequestStatus.Rejected;
        existingRequest.CurrentStepOrder = 0;

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

        _logger.LogInformation("Request {RequestId} has been REJECTED by {EmployeeId}. Reason: {Reason}",
            existingRequest.Id, employee.Id, request.Reason);

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