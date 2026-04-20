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
        _logger.LogInformation("[RequestReject] Starting — RequestId={RequestId}", request.RequestId);

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("[RequestReject] Unauthorized — no userId");
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);
        }

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogWarning("[RequestReject] Employee not found — UserId={UserId}", userId);
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);
        }

        _logger.LogInformation("[RequestReject] Rejecter resolved — Name={Name}", employee.FullName);

        var existingRequest = await _unitOfWork.Requests.GetByIdWithHistoryAsync(request.RequestId, cancellationToken);
        if (existingRequest == null)
        {
            _logger.LogWarning("[RequestReject] Request not found — RequestId={RequestId}", request.RequestId);
            return Result.Failure<bool>(DomainErrors.Requests.NotFound);
        }

        _logger.LogInformation("[RequestReject] Request loaded — Status={Status}, CurrentStep={Step}",
            existingRequest.Status, existingRequest.CurrentStepOrder);

        // 1. Status check
        if (existingRequest.Status != RequestStatus.Submitted && existingRequest.Status != RequestStatus.InProgress)
        {
            _logger.LogWarning("[RequestReject] Invalid status — RequestId={RequestId}, Status={Status}",
                request.RequestId, existingRequest.Status);
            return Result.Failure<bool>(DomainErrors.Requests.Locked);
        }

        // 2. Deserialize planned steps
        var plannedSteps = JsonSerializer.Deserialize<List<PlannedStepDto>>(existingRequest.PlannedStepsJson ?? "[]");
        if (plannedSteps == null || plannedSteps.Count == 0)
        {
            _logger.LogWarning("[RequestReject] No planned steps — RequestId={RequestId}", request.RequestId);
            return Result.Failure<bool>(DomainErrors.Request.InvalidWorkflowChain);
        }

        // 3. Validate step order bounds
        if (existingRequest.CurrentStepOrder < 1 || existingRequest.CurrentStepOrder > plannedSteps.Count)
        {
            _logger.LogWarning("[RequestReject] Step out of bounds — RequestId={RequestId}, Step={Step}, Total={Total}",
                request.RequestId, existingRequest.CurrentStepOrder, plannedSteps.Count);
            return Result.Failure<bool>(DomainErrors.Request.StepOrderExceeded);
        }

        // 4. Get current step and validate rejecter
        var currentStep = plannedSteps[existingRequest.CurrentStepOrder - 1];
        var approverIds = currentStep.Approvers.Select(a => a.EmployeeId).ToList();

        _logger.LogInformation("[RequestReject] Current step — Order={Order}, Node={Node}, Type={Type}",
            existingRequest.CurrentStepOrder, currentStep.NodeName, currentStep.StepType);

        if (!approverIds.Contains(employee.Id))
        {
            _logger.LogWarning("[RequestReject] Unauthorized — RequestId={RequestId}, RejecterId={RejecterId}",
                request.RequestId, employee.Id);
            return Result.Failure<bool>(DomainErrors.Requests.Unauthorized);
        }

        // 5. Add history record and transition to Rejected
        var history = new RequestApprovalHistory
        {
            RequestId = existingRequest.Id,
            ApproverId = employee.Id,
            Status = RequestStatus.Rejected,
            Comment = request.Reason
        };
        existingRequest.ApprovalHistory.Add(history);

        existingRequest.Status = RequestStatus.Rejected;
        existingRequest.CurrentStepOrder = 0;
        existingRequest.CurrentStepApproverIds = null;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[RequestReject] Request REJECTED — RequestId={RequestId}", request.RequestId);

        return Result.Success(true);
    }
}
