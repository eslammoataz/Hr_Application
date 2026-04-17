using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Features.Requests.Strategies;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.Requests.Commands.ApproveRequest;

public record ApproveRequestCommand(Guid RequestId, string? Comment) : IRequest<Result<bool>>;

public class ApproveRequestCommandHandler : IRequestHandler<ApproveRequestCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRequestStrategyFactory _strategyFactory;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ApproveRequestCommandHandler> _logger;

    public ApproveRequestCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IRequestStrategyFactory strategyFactory,
        INotificationService notificationService,
        ILogger<ApproveRequestCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _strategyFactory = strategyFactory;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(ApproveRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogWarning("ApproveRequest failed: Employee profile not found for UserId {UserId}", userId);
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);
        }

        _logger.LogInformation("Approver {EmployeeId} ({FullName}) attempting to approve request {RequestId}",
            employee.Id, employee.FullName, request.RequestId);

        var existingRequest = await _unitOfWork.Requests.GetByIdWithHistoryAsync(request.RequestId, cancellationToken);
        if (existingRequest == null)
            return Result.Failure<bool>(DomainErrors.Requests.NotFound);

        // 1. Status: Is it pending?
        if (existingRequest.Status != RequestStatus.Submitted && existingRequest.Status != RequestStatus.InProgress)
            return Result.Failure<bool>(DomainErrors.Requests.Locked);

        // 2. Deserialize the planned steps
        var plannedSteps = JsonSerializer.Deserialize<List<PlannedStepDto>>(existingRequest.PlannedStepsJson ?? "[]");
        if (plannedSteps == null || plannedSteps.Count == 0)
        {
            _logger.LogWarning("ApproveRequest failed: No planned steps found for request {RequestId}", request.RequestId);
            return Result.Failure<bool>(DomainErrors.Request.InvalidWorkflowChain);
        }

        // 3. Validate current step order is within bounds
        if (existingRequest.CurrentStepOrder < 1 || existingRequest.CurrentStepOrder > plannedSteps.Count)
        {
            _logger.LogWarning("ApproveRequest failed: Invalid step order {StepOrder} for request {RequestId}",
                existingRequest.CurrentStepOrder, request.RequestId);
            return Result.Failure<bool>(DomainErrors.Request.StepOrderExceeded);
        }

        // 4. Get current step and validate approver
        var currentStep = plannedSteps[existingRequest.CurrentStepOrder - 1];
        var approverIds = currentStep.Approvers.Select(a => a.EmployeeId).ToList();

        if (!approverIds.Contains(employee.Id))
        {
            _logger.LogWarning("Unauthorized approval attempt for request {RequestId} by {EmployeeId}. Not in current approver list.",
                request.RequestId, employee.Id);
            return Result.Failure<bool>(DomainErrors.Requests.Unauthorized);
        }

        // 5. Add history record
        var history = new RequestApprovalHistory
        {
            RequestId = existingRequest.Id,
            ApproverId = employee.Id,
            Status = RequestStatus.Approved,
            Comment = request.Comment
        };
        existingRequest.ApprovalHistory.Add(history);

        // 6. Advance to next step
        existingRequest.CurrentStepOrder++;

        // 7. Check if fully approved
        if (existingRequest.CurrentStepOrder > plannedSteps.Count)
        {
            existingRequest.Status = RequestStatus.Approved;
            existingRequest.CurrentStepOrder = 0;
            existingRequest.CurrentStepApproverIds = null;

            // Execute final actions via strategy
            var strategy = _strategyFactory.GetStrategy(existingRequest.RequestType);
            if (strategy != null)
            {
                _logger.LogInformation("Request {RequestId} ({FileType}) reached final approval. Executing strategy final actions.",
                    existingRequest.Id, existingRequest.RequestType);
                await strategy.OnFinalApprovalAsync(existingRequest, cancellationToken);
            }

            _logger.LogInformation("Request {RequestId} has been FULLY APPROVED.", existingRequest.Id);
        }
        else
        {
            existingRequest.Status = RequestStatus.InProgress;
            // Update denormalized approver IDs for the new step
            var nextStep = plannedSteps[existingRequest.CurrentStepOrder - 1];
            existingRequest.CurrentStepApproverIds = string.Join(",", nextStep.Approvers.Select(a => a.EmployeeId));
            _logger.LogInformation("Request {RequestId} moved to step {StepOrder} of {TotalSteps}",
                existingRequest.Id, existingRequest.CurrentStepOrder, plannedSteps.Count);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (existingRequest.Status == RequestStatus.Approved)
        {
            try
            {
                await _notificationService.SendNotificationAsync(
                    existingRequest.EmployeeId,
                    "Request Approved",
                    $"Your {existingRequest.RequestType} request has been approved.",
                    NotificationType.RequestApproved);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Request {RequestId} approved, but notification delivery failed.", existingRequest.Id);
            }
        }

        return Result.Success(true);
    }
}