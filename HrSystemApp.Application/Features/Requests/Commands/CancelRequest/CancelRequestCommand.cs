using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Application.Features.Requests.Commands.CancelRequest;

public record CancelRequestCommand(Guid Id) : IRequest<Result<bool>>;

public class CancelRequestCommandHandler : IRequestHandler<CancelRequestCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CancelRequestCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public CancelRequestCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<CancelRequestCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<bool>> Handle(CancelRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CancelRequest, LogStage.Authorization,
                "UserNotAuthenticated", null);
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);
        }

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CancelRequest, LogStage.Authorization,
                "EmployeeNotFound", new { UserId = userId });
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);
        }

        var existingRequest = await _unitOfWork.Requests.GetByIdAsync(request.Id, cancellationToken);
        if (existingRequest == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CancelRequest, LogStage.Validation,
                "RequestNotFound", new { RequestId = request.Id });
            return Result.Failure<bool>(DomainErrors.Requests.NotFound);
        }

        if (existingRequest.EmployeeId != employee.Id)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CancelRequest, LogStage.Authorization,
                "UnauthorizedCancel", new { RequestId = request.Id, EmployeeId = employee.Id });
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);
        }

        if (existingRequest.Status != RequestStatus.Submitted)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CancelRequest, LogStage.Validation,
                "InvalidStatusForCancel", new { RequestId = request.Id, CurrentStatus = existingRequest.Status.ToString() });
            return Result.Failure<bool>(DomainErrors.Requests.InvalidStatusForOperation);
        }

        existingRequest.Status = RequestStatus.Cancelled;
        await _unitOfWork.Requests.UpdateAsync(existingRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogBusinessFlow(_loggingOptions, LogAction.Workflow.CancelRequest, LogStage.Processing,
            "RequestCancelled", new { RequestId = request.Id, EmployeeId = employee.Id });

        return Result.Success(true);
    }
}
