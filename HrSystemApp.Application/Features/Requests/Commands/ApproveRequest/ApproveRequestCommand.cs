using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Requests.Strategies;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Application.Features.Requests.Commands.ApproveRequest;

public record ApproveRequestCommand(Guid RequestId, string? Comment) : IRequest<Result<bool>>, IHaveRequestId
{
    Guid IHaveRequestId.RequestId => RequestId;
}

public class ApproveRequestCommandHandler : IRequestHandler<ApproveRequestCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRequestStrategyFactory _strategyFactory;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ApproveRequestCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public ApproveRequestCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IRequestStrategyFactory strategyFactory,
        INotificationService notificationService,
        ILogger<ApproveRequestCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _strategyFactory = strategyFactory;
        _notificationService = notificationService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<bool>> Handle(ApproveRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarningUnauthorized(_loggingOptions, LogAction.Workflow.ApproveRequest);
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);
        }

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogWarningUnauthorized(_loggingOptions, LogAction.Workflow.ApproveRequest);
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);
        }

        _logger.LogBusinessFlow(_loggingOptions, LogAction.Workflow.ApproveRequest, LogStage.Authorization,
            "ApproverResolved", new { EmployeeId = employee.Id });

        var existingRequest = await _unitOfWork.Requests.GetByIdWithHistoryAsync(request.RequestId, cancellationToken);
        if (existingRequest == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.ApproveRequest, LogStage.Authorization,
                "RequestNotFound", new { RequestId = request.RequestId });
            return Result.Failure<bool>(DomainErrors.Requests.NotFound);
        }

        if (existingRequest.Status != RequestStatus.Submitted && existingRequest.Status != RequestStatus.InProgress)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.ApproveRequest, LogStage.Authorization,
                "InvalidStatus", new { RequestId = request.RequestId, Status = existingRequest.Status.ToString() });
            return Result.Failure<bool>(DomainErrors.Requests.Locked);
        }

        var plannedSteps = JsonSerializer.Deserialize<List<PlannedStepDto>>(existingRequest.PlannedStepsJson ?? "[]");
        if (plannedSteps == null || plannedSteps.Count == 0)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.ApproveRequest, LogStage.Authorization,
                "NoPlannedSteps", new { RequestId = request.RequestId });
            return Result.Failure<bool>(DomainErrors.Request.InvalidWorkflowChain);
        }

        if (existingRequest.CurrentStepOrder < 1 || existingRequest.CurrentStepOrder > plannedSteps.Count)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.ApproveRequest, LogStage.Authorization,
                "StepOutOfBounds", new { RequestId = request.RequestId, CurrentStep = existingRequest.CurrentStepOrder, TotalSteps = plannedSteps.Count });
            return Result.Failure<bool>(DomainErrors.Request.StepOrderExceeded);
        }

        var currentStep = plannedSteps[existingRequest.CurrentStepOrder - 1];
        var approverIds = currentStep.Approvers.Select(a => a.EmployeeId).ToList();

        if (!approverIds.Contains(employee.Id))
        {
            _logger.LogWarningUnauthorized(_loggingOptions, LogAction.Workflow.ApproveRequest);
            return Result.Failure<bool>(DomainErrors.Requests.Unauthorized);
        }

        _logger.LogBusinessFlow(_loggingOptions, LogAction.Workflow.ApproveRequest, LogStage.Authorization,
            "ApproverAuthorized", new { EmployeeId = employee.Id, StepOrder = existingRequest.CurrentStepOrder });

        var history = new RequestApprovalHistory
        {
            RequestId = existingRequest.Id,
            ApproverId = employee.Id,
            Status = RequestStatus.Approved,
            Comment = request.Comment ?? ""
        };
        existingRequest.ApprovalHistory.Add(history);

        existingRequest.CurrentStepOrder++;

        var isFullyApproved = existingRequest.CurrentStepOrder > plannedSteps.Count;

        _logger.LogBusinessFlow(_loggingOptions, LogAction.Workflow.ApproveRequest, LogStage.Processing,
            isFullyApproved ? "FullyApproved" : "StepAdvanced",
            new { NewStepOrder = existingRequest.CurrentStepOrder, TotalSteps = plannedSteps.Count });

        if (isFullyApproved)
        {
            existingRequest.Status = RequestStatus.Approved;
            existingRequest.CurrentStepOrder = 0;
            existingRequest.CurrentStepApproverIds = null;

            var strategy = _strategyFactory.GetStrategy(existingRequest.RequestType);
            if (strategy != null)
            {
                await strategy.OnFinalApprovalAsync(existingRequest, cancellationToken);
            }
        }
        else
        {
            var nextStep = plannedSteps[existingRequest.CurrentStepOrder - 1];
            existingRequest.Status = RequestStatus.InProgress;
            existingRequest.CurrentStepApproverIds = string.Join(",",
                nextStep.Approvers.Select(a => a.EmployeeId.ToString()));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}