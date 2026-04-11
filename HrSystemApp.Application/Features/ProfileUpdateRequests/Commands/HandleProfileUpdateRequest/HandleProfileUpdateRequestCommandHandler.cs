using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HrSystemApp.Application.Features.ProfileUpdateRequests.Commands.HandleProfileUpdateRequest;

public class HandleProfileUpdateRequestCommandHandler : IRequestHandler<HandleProfileUpdateRequestCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<HandleProfileUpdateRequestCommandHandler> _logger;

    public HandleProfileUpdateRequestCommandHandler(IUnitOfWork unitOfWork,
        ILogger<HandleProfileUpdateRequestCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(HandleProfileUpdateRequestCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling profile update request '{RequestId}' by HR user '{HrUserId}' with decision '{Decision}'.",
            command.RequestId, command.HrUserId, command.Dto.IsAccepted ? "Approved" : "Rejected");

        // ── 1. Validate request exists and is still pending ──────────────────
        var request = await _unitOfWork.ProfileUpdateRequests.GetByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            _logger.LogWarning("Profile update request '{RequestId}' not found.", command.RequestId);
            return Result.Failure(DomainErrors.ProfileUpdate.NotFound);
        }

        if (request.Status != "Pending")
        {
            _logger.LogWarning(
                "Profile update request '{RequestId}' is not in Pending status. Current status: '{Status}'.",
                command.RequestId, request.Status);
            return Result.Failure(DomainErrors.ProfileUpdate.NotPending);
        }

        // ── 2. Validate HR employee exists ───────────────────────────────────
        var hrEmployee = await _unitOfWork.Employees.GetByUserIdAsync(command.HrUserId, cancellationToken);
        if (hrEmployee is null)
        {
            _logger.LogWarning("HR user '{HrUserId}' has no employee record.", command.HrUserId);
            return Result.Failure(DomainErrors.Hr.EmployeeNotFound);
        }

        // ── 3. Pre-validate everything needed for approval ───────────────────
        //       All validation is done BEFORE opening the transaction so we
        //       hold DB resources for the shortest possible time.
        Employee? employee = null;
        Dictionary<string, JsonElement>? changes = null;

        if (command.Dto.IsAccepted)
        {
            employee = await _unitOfWork.Employees.GetWithDetailsAsync(request.EmployeeId, cancellationToken);
            if (employee is null)
            {
                _logger.LogError("Employee with ID '{EmployeeId}' not found during approval of request '{RequestId}'.",
                    request.EmployeeId, command.RequestId);
                return Result.Failure(DomainErrors.ProfileUpdate.EmployeeNotFound);
            }

            if (string.IsNullOrWhiteSpace(request.ChangesJson))
            {
                _logger.LogError("ChangesJson is empty for approved request '{RequestId}'.", command.RequestId);
                return Result.Failure(DomainErrors.ProfileUpdate.EmptyChanges);
            }

            changes = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.ChangesJson);
            if (changes is null)
            {
                _logger.LogError("Failed to deserialize ChangesJson for request '{RequestId}'. Data: {ChangesJson}",
                    command.RequestId, request.ChangesJson);
                return Result.Failure(DomainErrors.ProfileUpdate.DeserializationFailed);
            }

            // Validate and apply all field changes before touching the DB
            foreach (var change in changes)
            {
                if (!change.Value.TryGetProperty("newValue", out var newValueElement))
                {
                    _logger.LogError(
                        "Missing 'newValue' key in ChangesJson for field '{Field}' in request '{RequestId}'.",
                        change.Key, command.RequestId);
                    return Result.Failure(DomainErrors.ProfileUpdate.MalformedChanges);
                }

                var applyResult = ApplyFieldChange(employee, change.Key, newValueElement.GetString());
                if (applyResult.IsFailure)
                {
                    _logger.LogError("Failed to apply field change for field '{Field}' with error: {Error}", change.Key,
                        applyResult.Error.Message);
                    return applyResult;
                }
            }
        }

        // ── 4. Open transaction — only DB writes from this point ─────────────
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Beginning transaction for handling request '{RequestId}'.", command.RequestId);

            request.Status = command.Dto.IsAccepted ? "Approved" : "Rejected";
            request.HrNote = command.Dto.HrNote;
            request.HandledAt = DateTime.UtcNow;
            request.HandledByHrId = hrEmployee.Id;
            request.UpdatedById = command.HrUserId;

            if (employee is not null)
                await _unitOfWork.Employees.UpdateAsync(employee, cancellationToken);

            await _unitOfWork.ProfileUpdateRequests.UpdateAsync(request, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation("Successfully handled profile update request '{RequestId}'.", command.RequestId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "An error occurred while handling profile update request '{RequestId}'. Rolling back transaction.",
                command.RequestId);
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