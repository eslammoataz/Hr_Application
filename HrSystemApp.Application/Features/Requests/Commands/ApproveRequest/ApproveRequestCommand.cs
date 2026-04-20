using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Requests.Strategies;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
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
        _logger.LogInformation("[RequestApprove] Starting — RequestId={RequestId}", request.RequestId);

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("[RequestApprove] Unauthorized — no userId");
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);
        }

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogWarning("[RequestApprove] Approver not found — UserId={UserId}", userId);
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);
        }

        _logger.LogInformation("[RequestApprove] Approver resolved — EmployeeId={EmployeeId}, Name={Name}",
            employee.Id, employee.FullName);

        var existingRequest = await _unitOfWork.Requests.GetByIdWithHistoryAsync(request.RequestId, cancellationToken);
        if (existingRequest == null)
        {
            _logger.LogWarning("[RequestApprove] Request not found — RequestId={RequestId}", request.RequestId);
            return Result.Failure<bool>(DomainErrors.Requests.NotFound);
        }

        _logger.LogInformation("[RequestApprove] Request loaded — Status={Status}, CurrentStep={Step}",
            existingRequest.Status, existingRequest.CurrentStepOrder);

        // 1. Status check
        if (existingRequest.Status != RequestStatus.Submitted && existingRequest.Status != RequestStatus.InProgress)
        {
            _logger.LogWarning("[RequestApprove] Invalid status — RequestId={RequestId}, Status={Status}",
                request.RequestId, existingRequest.Status);
            return Result.Failure<bool>(DomainErrors.Requests.Locked);
        }

        // 2. Deserialize planned steps
        var plannedSteps = JsonSerializer.Deserialize<List<PlannedStepDto>>(existingRequest.PlannedStepsJson ?? "[]");
        if (plannedSteps == null || plannedSteps.Count == 0)
        {
            _logger.LogWarning("[RequestApprove] No planned steps — RequestId={RequestId}", request.RequestId);
            return Result.Failure<bool>(DomainErrors.Request.InvalidWorkflowChain);
        }

        // 3. Validate step order bounds
        if (existingRequest.CurrentStepOrder < 1 || existingRequest.CurrentStepOrder > plannedSteps.Count)
        {
            _logger.LogWarning("[RequestApprove] Step out of bounds — RequestId={RequestId}, Step={Step}, Total={Total}",
                request.RequestId, existingRequest.CurrentStepOrder, plannedSteps.Count);
            return Result.Failure<bool>(DomainErrors.Request.StepOrderExceeded);
        }

        // 4. Get current step and validate approver
        var currentStep = plannedSteps[existingRequest.CurrentStepOrder - 1];
        var approverIds = currentStep.Approvers.Select(a => a.EmployeeId).ToList();

        _logger.LogInformation("[RequestApprove] Current step — Order={Order}, Node={Node}, Type={Type}, Approvers={Approvers}",
            existingRequest.CurrentStepOrder, currentStep.NodeName, currentStep.StepType, approverIds.Count);

        if (!approverIds.Contains(employee.Id))
        {
            _logger.LogWarning("[RequestApprove] Unauthorized — RequestId={RequestId}, EmployeeId={EmployeeId}",
                request.RequestId, employee.Id);
            return Result.Failure<bool>(DomainErrors.Requests.Unauthorized);
        }

        // 5. Add history record
        var history = new RequestApprovalHistory
        {
            RequestId = existingRequest.Id,
            ApproverId = employee.Id,
            Status = RequestStatus.Approved,
            Comment = request.Comment ?? ""
        };
        existingRequest.ApprovalHistory.Add(history);

        // 6. Advance step
        existingRequest.CurrentStepOrder++;

        _logger.LogInformation("[RequestApprove] Step advanced — NewStep={NewStep}, TotalSteps={Total}",
            existingRequest.CurrentStepOrder, plannedSteps.Count);

        // 7. Final approval check
        if (existingRequest.CurrentStepOrder > plannedSteps.Count)
        {
            _logger.LogInformation("[RequestApprove] FINAL APPROVAL — RequestId={RequestId}", request.RequestId);

            existingRequest.Status = RequestStatus.Approved;
            existingRequest.CurrentStepOrder = 0;
            existingRequest.CurrentStepApproverIds = null;

            var strategy = _strategyFactory.GetStrategy(existingRequest.RequestType);
            if (strategy != null)
            {
                _logger.LogInformation("[RequestApprove] Executing OnFinalApprovalAsync — RequestId={RequestId}", existingRequest.Id);
                await strategy.OnFinalApprovalAsync(existingRequest, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("[RequestApprove] Request APPROVED — RequestId={RequestId}, Type={Type}",
                existingRequest.Id, existingRequest.RequestType);
            return Result.Success(true);
        }

        // 8. Move to next step — update CurrentStepApproverIds for the new step's approvers
        var nextStep = plannedSteps[existingRequest.CurrentStepOrder - 1];
        existingRequest.Status = RequestStatus.InProgress;
        existingRequest.CurrentStepApproverIds = string.Join(",",
            nextStep.Approvers.Select(a => a.EmployeeId.ToString()));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[RequestApprove] Advanced to step {Step} — RequestId={RequestId}",
            existingRequest.CurrentStepOrder, request.RequestId);

        return Result.Success(true);
    }
}
