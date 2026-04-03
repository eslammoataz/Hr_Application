using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Features.Requests.Strategies;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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

        // 1. Security: Is this the current approver?
        if (existingRequest.CurrentApproverId != employee.Id)
        {
            _logger.LogWarning("Unauthorized approval attempt for request {RequestId} by {EmployeeId}. Current expected approver: {ExpectedId}", 
                request.RequestId, employee.Id, existingRequest.CurrentApproverId);
            return Result.Failure<bool>(DomainErrors.Requests.Unauthorized);
        }

        // 2. Status: Is it in-progress or submitted?
        if (existingRequest.Status != RequestStatus.Submitted && existingRequest.Status != RequestStatus.InProgress)
            return Result.Failure<bool>(DomainErrors.Requests.Locked);

        // 3. Current history entry
        var history = new RequestApprovalHistory
        {
            RequestId = existingRequest.Id,
            ApproverId = employee.Id,
            Status = RequestStatus.Approved,
            Comment = request.Comment
        };
        existingRequest.ApprovalHistory.Add(history);

        // 4. Determine Next Approver from Snapshot
        var chain = JsonSerializer.Deserialize<List<ApproverSnapshot>>(existingRequest.PlannedChainJson ?? "[]");
        var currentIndex = chain?.FindIndex(a => a.Id == employee.Id) ?? -1;

        if (chain != null && currentIndex != -1 && currentIndex < chain.Count - 1)
        {
            // Move to next approver
            existingRequest.CurrentApproverId = chain[currentIndex + 1].Id;
            existingRequest.Status = RequestStatus.InProgress;
            
            _logger.LogInformation("Request {RequestId} moved to next stage. New current approver: {NextId} ({NextName})", 
                existingRequest.Id, existingRequest.CurrentApproverId, chain[currentIndex + 1].FullName);
        }
        else
        {
            existingRequest.CurrentApproverId = null;
            existingRequest.Status = RequestStatus.Approved;

            // Final Actions: e.g. Deduct Leave Balance using dynamic strategy
            var strategy = _strategyFactory.GetStrategy(existingRequest.RequestType);
            if (strategy != null)
            {
                _logger.LogInformation("Request {RequestId} ({FileType}) reached final approval. Executing strategy final actions.", 
                    existingRequest.Id, existingRequest.RequestType);
                await strategy.OnFinalApprovalAsync(existingRequest, cancellationToken);
            }
            
            _logger.LogInformation("Request {RequestId} has been FULLY APPROVED.", existingRequest.Id);
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


    private class ApproverSnapshot
    {
        public Guid Id { get; init; }
        public string? FullName { get; init; }
    }
}
