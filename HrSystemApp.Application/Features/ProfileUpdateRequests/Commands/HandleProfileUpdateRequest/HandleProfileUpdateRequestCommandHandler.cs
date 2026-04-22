using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace HrSystemApp.Application.Features.ProfileUpdateRequests.Commands.HandleProfileUpdateRequest;

public class HandleProfileUpdateRequestCommandHandler : IRequestHandler<HandleProfileUpdateRequestCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<HandleProfileUpdateRequestCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public HandleProfileUpdateRequestCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<HandleProfileUpdateRequestCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result> Handle(HandleProfileUpdateRequestCommand command, CancellationToken cancellationToken)
    {

        var request = await _unitOfWork.ProfileUpdateRequests.GetByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.HandleProfileUpdateRequest, LogStage.Validation,
                "RequestNotFound", new { RequestId = command.RequestId });
            return Result.Failure(DomainErrors.ProfileUpdate.NotFound);
        }

        if (request.Status != ProfileUpdateRequestStatus.Pending)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.HandleProfileUpdateRequest, LogStage.Validation,
                "RequestNotPending", new { RequestId = command.RequestId, Status = request.Status.ToString() });
            return Result.Failure(DomainErrors.ProfileUpdate.NotPending);
        }

        var hrEmployee = await _unitOfWork.Employees.GetByUserIdAsync(command.HrUserId, cancellationToken);
        if (hrEmployee is null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.HandleProfileUpdateRequest, LogStage.Authorization,
                "HrEmployeeNotFound", new { HrUserId = command.HrUserId });
            return Result.Failure(DomainErrors.Hr.EmployeeNotFound);
        }

        Employee? employee = null;
        Dictionary<string, JsonElement>? changes = null;

        if (command.Dto.IsAccepted)
        {
            employee = await _unitOfWork.Employees.GetWithDetailsAsync(request.EmployeeId, cancellationToken);
            if (employee is null)
            {
                _logger.LogDecision(_loggingOptions, LogAction.Workflow.HandleProfileUpdateRequest, LogStage.Validation,
                    "EmployeeNotFound", new { EmployeeId = request.EmployeeId, RequestId = command.RequestId });
                return Result.Failure(DomainErrors.ProfileUpdate.EmployeeNotFound);
            }

            if (string.IsNullOrWhiteSpace(request.ChangesJson))
            {
                _logger.LogDecision(_loggingOptions, LogAction.Workflow.HandleProfileUpdateRequest, LogStage.Validation,
                    "EmptyChangesJson", new { RequestId = command.RequestId });
                return Result.Failure(DomainErrors.ProfileUpdate.EmptyChanges);
            }

            changes = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.ChangesJson);
            if (changes is null)
            {
                _logger.LogDecision(_loggingOptions, LogAction.Workflow.HandleProfileUpdateRequest, LogStage.Validation,
                    "DeserializationFailed", new { RequestId = command.RequestId, ChangesJson = request.ChangesJson });
                return Result.Failure(DomainErrors.ProfileUpdate.DeserializationFailed);
            }

            foreach (var change in changes)
            {
                if (!change.Value.TryGetProperty("newValue", out var newValueElement))
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.HandleProfileUpdateRequest, LogStage.Validation,
                        "MissingNewValue", new { Field = change.Key, RequestId = command.RequestId });
                    return Result.Failure(DomainErrors.ProfileUpdate.MalformedChanges);
                }

                var applyResult = ApplyFieldChange(employee, change.Key, newValueElement.GetString());
                if (applyResult.IsFailure)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.HandleProfileUpdateRequest, LogStage.Validation,
                        "ApplyFieldChangeFailed", new { Field = change.Key, Error = applyResult.Error.Message });
                    return applyResult;
                }
            }
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.HandleProfileUpdateRequest, LogStage.Processing,
                "TransactionStarted", new { RequestId = command.RequestId });

            request.Status = command.Dto.IsAccepted ? ProfileUpdateRequestStatus.Approved : ProfileUpdateRequestStatus.Rejected;
            request.HrNote = command.Dto.HrNote;
            request.HandledAt = DateTime.UtcNow;
            request.HandledByHrId = hrEmployee.Id;
            request.UpdatedById = command.HrUserId;

            if (employee is not null)
                await _unitOfWork.Employees.UpdateAsync(employee, cancellationToken);

            await _unitOfWork.ProfileUpdateRequests.UpdateAsync(request, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogActionFailure(_loggingOptions, LogAction.Workflow.HandleProfileUpdateRequest, LogStage.Processing, ex,
                new { RequestId = command.RequestId });
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private static Result ApplyFieldChange(Employee employee, string field, string? newValue)
    {
        switch (field)
        {
            case nameof(Employee.PhoneNumber):
                employee.PhoneNumber = newValue ?? string.Empty;
                if (employee.User is not null)
                    employee.User.PhoneNumber = newValue ?? string.Empty;
                break;

            case nameof(Employee.Address):
                employee.Address = newValue;
                break;

            case nameof(Employee.FullName):
                employee.FullName = newValue ?? string.Empty;
                if (employee.User is not null)
                    employee.User.Name = newValue ?? string.Empty;
                break;

            case nameof(Employee.CompanyLocationId):
                if (!string.IsNullOrEmpty(newValue))
                {
                    if (!Guid.TryParse(newValue, out var locationId))
                        return Result.Failure(DomainErrors.ProfileUpdate.InvalidLocationId);
                    employee.CompanyLocationId = locationId;
                }
                else
                {
                    employee.CompanyLocationId = null;
                }

                break;

            default:
                return Result.Failure(DomainErrors.ProfileUpdate.UnknownField);
        }

        return Result.Success();
    }
}