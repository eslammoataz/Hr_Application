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

namespace HrSystemApp.Application.Features.Requests.Commands.DeleteRequest;

public record DeleteRequestCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteRequestCommandHandler : IRequestHandler<DeleteRequestCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DeleteRequestCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public DeleteRequestCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<DeleteRequestCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<bool>> Handle(DeleteRequestCommand request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.Workflow.DeleteRequest);

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.DeleteRequest, LogStage.Authorization,
                "UserNotAuthenticated", null);
            sw.Stop();
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);
        }

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.DeleteRequest, LogStage.Authorization,
                "EmployeeNotFound", new { UserId = userId });
            sw.Stop();
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);
        }

        var existingRequest = await _unitOfWork.Requests.GetByIdWithHistoryAsync(request.Id, cancellationToken);
        if (existingRequest == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.DeleteRequest, LogStage.Validation,
                "RequestNotFound", new { RequestId = request.Id });
            sw.Stop();
            return Result.Failure<bool>(DomainErrors.Requests.NotFound);
        }

        if (existingRequest.EmployeeId != employee.Id)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.DeleteRequest, LogStage.Authorization,
                "UnauthorizedDelete", new { RequestId = request.Id, EmployeeId = employee.Id });
            sw.Stop();
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);
        }

        if (existingRequest.Status == RequestStatus.Approved)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.DeleteRequest, LogStage.Validation,
                "RequestAlreadyApproved", new { RequestId = request.Id });
            sw.Stop();
            return Result.Failure<bool>(DomainErrors.Requests.ModificationLocked);
        }

        if (existingRequest.ApprovalHistory.Any())
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.DeleteRequest, LogStage.Validation,
                "RequestHasHistory", new { RequestId = request.Id });
            sw.Stop();
            return Result.Failure<bool>(DomainErrors.Requests.ModificationLocked);
        }

        await _unitOfWork.Requests.DeleteAsync(existingRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.Workflow.DeleteRequest, sw.ElapsedMilliseconds);

        return Result.Success(true);
    }
}
