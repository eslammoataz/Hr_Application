using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Application.Features.Requests.Commands.RejectRequest;

public record RejectRequestCommand(Guid RequestId, string Reason) : IRequest<Result<bool>>, IHaveRequestId
{
    Guid IHaveRequestId.RequestId => RequestId;
}

public class RejectRequestCommandHandler : IRequestHandler<RejectRequestCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<RejectRequestCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public RejectRequestCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        INotificationService notificationService,
        ILogger<RejectRequestCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<bool>> Handle(RejectRequestCommand request, CancellationToken cancellationToken)
    {

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarningUnauthorized(_loggingOptions, LogAction.Workflow.RejectRequest);
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);
        }

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogWarningUnauthorized(_loggingOptions, LogAction.Workflow.RejectRequest);
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);
        }

        _logger.LogDecision(_loggingOptions, LogAction.Workflow.RejectRequest, LogStage.Authorization,
            "RejecterResolved", new { EmployeeId = employee.Id });

        var existingRequest = await _unitOfWork.Requests.GetByIdWithHistoryAsync(request.RequestId, cancellationToken);
        if (existingRequest == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.RejectRequest, LogStage.Authorization,
                "RequestNotFound", new { RequestId = request.RequestId });
            return Result.Failure<bool>(DomainErrors.Requests.NotFound);
        }

        if (existingRequest.Status != RequestStatus.Submitted && existingRequest.Status != RequestStatus.InProgress)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.RejectRequest, LogStage.Authorization,
                "InvalidStatus", new { RequestId = request.RequestId, Status = existingRequest.Status.ToString() });
            return Result.Failure<bool>(DomainErrors.Requests.Locked);
        }

        var plannedSteps = JsonSerializer.Deserialize<List<PlannedStepDto>>(existingRequest.PlannedStepsJson ?? "[]");
        if (plannedSteps == null || plannedSteps.Count == 0)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.RejectRequest, LogStage.Authorization,
                "NoPlannedSteps", new { RequestId = request.RequestId });
            return Result.Failure<bool>(DomainErrors.Request.InvalidWorkflowChain);
        }

        if (existingRequest.CurrentStepOrder < 1 || existingRequest.CurrentStepOrder > plannedSteps.Count)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.RejectRequest, LogStage.Authorization,
                "StepOutOfBounds", new { RequestId = request.RequestId, CurrentStep = existingRequest.CurrentStepOrder, TotalSteps = plannedSteps.Count });
            return Result.Failure<bool>(DomainErrors.Request.StepOrderExceeded);
        }

        var currentStep = plannedSteps[existingRequest.CurrentStepOrder - 1];
        var approverIds = currentStep.Approvers.Select(a => a.EmployeeId).ToList();

        if (!approverIds.Contains(employee.Id))
        {
            _logger.LogWarningUnauthorized(_loggingOptions, LogAction.Workflow.RejectRequest);
            return Result.Failure<bool>(DomainErrors.Requests.Unauthorized);
        }

        _logger.LogDecision(_loggingOptions, LogAction.Workflow.RejectRequest, LogStage.Authorization,
            "RejecterAuthorized", new { EmployeeId = employee.Id, StepOrder = existingRequest.CurrentStepOrder });

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

        _logger.LogDecision(_loggingOptions, LogAction.Workflow.RejectRequest, LogStage.Finalization,
            "RequestRejected", new { RequestId = request.RequestId, EmployeeId = employee.Id });

        return Result.Success(true);
    }
}
