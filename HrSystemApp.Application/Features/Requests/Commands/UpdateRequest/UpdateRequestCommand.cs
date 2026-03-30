using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HrSystemApp.Application.Features.Requests.Commands.UpdateRequest;

public record UpdateRequestCommand(Guid Id, RequestType RequestType, JsonElement Data, string? Details = null) : IRequest<Result<Guid>>;

public class UpdateRequestCommandHandler : IRequestHandler<UpdateRequestCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UpdateRequestCommandHandler> _logger;

    public UpdateRequestCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, ILogger<UpdateRequestCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(UpdateRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
            return Result.Failure<Guid>(DomainErrors.Employee.NotFound);

        var existingRequest = await _unitOfWork.Requests.GetByIdWithHistoryAsync(request.Id, cancellationToken);
        if (existingRequest == null)
            return Result.Failure<Guid>(DomainErrors.Requests.NotFound);

        // 1. Security: Only owner can edit
        if (existingRequest.EmployeeId != employee.Id)
        {
            _logger.LogWarning("Unauthorized edit attempt for request {RequestId} by user {UserId}", request.Id, userId);
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);
        }

        // 2. Status check: No actions must have been taken
        if (existingRequest.ApprovalHistory.Any())
        {
            _logger.LogWarning("Edit attempt failed: Request {RequestId} already has approval history and is locked.", request.Id);
            return Result.Failure<Guid>(DomainErrors.Requests.ModificationLocked);
        }

        if (existingRequest.Status != RequestStatus.Submitted)
        {
            _logger.LogWarning("Edit attempt failed: Request {RequestId} status is {Status} and cannot be edited.", request.Id, existingRequest.Status);
            return Result.Failure<Guid>(DomainErrors.Requests.NotPending);
        }

        // 3. Apply updates
        _logger.LogInformation("Updating request {RequestId} for employee {EmployeeId}. Old Type: {OldType}, New Type: {NewType}", 
            request.Id, employee.Id, existingRequest.RequestType, request.RequestType);

        existingRequest.RequestType = request.RequestType;
        existingRequest.Data = request.Data.GetRawText();
        existingRequest.Details = request.Details;
        existingRequest.UpdatedAt = DateTime.UtcNow;

        // Note: We don't call UpdateAsync here because it's a tracked entity from GetByIdWithHistoryAsync
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(existingRequest.Id);
    }
}
