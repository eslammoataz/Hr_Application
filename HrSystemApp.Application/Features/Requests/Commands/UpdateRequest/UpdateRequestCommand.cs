using System.Diagnostics;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace HrSystemApp.Application.Features.Requests.Commands.UpdateRequest;

public record UpdateRequestCommand(Guid Id, RequestType RequestType, JsonElement Data, string? Details = null) : IRequest<Result<Guid>>;

public class UpdateRequestCommandHandler : IRequestHandler<UpdateRequestCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UpdateRequestCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public UpdateRequestCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<UpdateRequestCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<Guid>> Handle(UpdateRequestCommand request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.Workflow.UpdateRequest);

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequest, LogStage.Authorization,
                "UserNotAuthenticated", null);
            sw.Stop();
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);
        }

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequest, LogStage.Authorization,
                "EmployeeNotFound", new { UserId = userId });
            sw.Stop();
            return Result.Failure<Guid>(DomainErrors.Employee.NotFound);
        }

        var existingRequest = await _unitOfWork.Requests.GetByIdWithHistoryAsync(request.Id, cancellationToken);
        if (existingRequest == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequest, LogStage.Validation,
                "RequestNotFound", new { RequestId = request.Id });
            sw.Stop();
            return Result.Failure<Guid>(DomainErrors.Requests.NotFound);
        }

        if (existingRequest.EmployeeId != employee.Id)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequest, LogStage.Authorization,
                "UnauthorizedEdit", new { RequestId = request.Id, UserId = userId });
            sw.Stop();
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);
        }

        if (existingRequest.ApprovalHistory.Any())
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequest, LogStage.Validation,
                "RequestLockedDueToHistory", new { RequestId = request.Id });
            sw.Stop();
            return Result.Failure<Guid>(DomainErrors.Requests.ModificationLocked);
        }

        if (existingRequest.Status != RequestStatus.Submitted)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateRequest, LogStage.Validation,
                "RequestNotPending", new { RequestId = request.Id, Status = existingRequest.Status.ToString() });
            sw.Stop();
            return Result.Failure<Guid>(DomainErrors.Requests.NotPending);
        }

        existingRequest.RequestType = request.RequestType;
        existingRequest.Data = request.Data.GetRawText();
        existingRequest.Details = request.Details;
        existingRequest.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.Workflow.UpdateRequest, sw.ElapsedMilliseconds);

        return Result.Success(existingRequest.Id);
    }
}
