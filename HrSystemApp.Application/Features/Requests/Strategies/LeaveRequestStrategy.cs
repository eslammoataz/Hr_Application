using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.Requests.Strategies;

public class LeaveRequestStrategy : IRequestBusinessStrategy
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LeaveRequestStrategy> _logger;

    public LeaveRequestStrategy(IUnitOfWork unitOfWork, ILogger<LeaveRequestStrategy> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public RequestType Type => RequestType.Leave;

    public async Task<Result> ValidateBusinessRulesAsync(Guid employeeId, JsonElement data, CancellationToken ct)
    {
        // 1. Extract Data
        var leaveSubType = (LeaveType)data.GetProperty("leaveSubType").GetInt32();
        var startDateTime = data.GetProperty("startDateTime").GetDateTime();
        var duration = data.GetProperty("duration").GetDecimal();
        var isHourly = data.TryGetProperty("isHourly", out var ih) && ih.GetBoolean();

        if (duration <= 0)
            return Result.Failure(new Error("Request.InvalidDuration", "Duration must be positive."));

        // 2. Validate Balance (Only for full-day leaves)
        if (!isHourly)
        {
            var balance = await _unitOfWork.LeaveBalances.GetAsync(employeeId, leaveSubType, startDateTime.Year, ct);
            if (balance == null)
                return Result.Failure(new Error("LeaveBalance.NotFound", "No leave balance found for this employee and year."));

            var allPendingLeave = await _unitOfWork.Requests.FindAsync(r =>
                r.EmployeeId == employeeId &&
                r.RequestType == RequestType.Leave &&
                (r.Status == RequestStatus.Submitted || r.Status == RequestStatus.InProgress), ct);

            // Calculate pending duration by parsing JSON data of other requests
            decimal pendingDuration = 0;
            foreach (var req in allPendingLeave)
            {
                using var doc = JsonDocument.Parse(req.Data);
                var reqData = doc.RootElement;
                var reqIsHourly = reqData.TryGetProperty("isHourly", out var rih) && rih.GetBoolean();
                if (!reqIsHourly)
                {
                    pendingDuration += reqData.GetProperty("duration").GetDecimal();
                }
            }

            if (balance.RemainingDays - pendingDuration < duration)
            {
                var msg = $"Insufficient leave balance. Available: {balance.RemainingDays}, Pending: {pendingDuration}. Requested: {duration}.";
                _logger.LogWarning("Insufficient balance for Employee {EmployeeId}. {Msg}", employeeId, msg);
                return Result.Failure(new Error("LeaveBalance.Insufficient", msg));
            }
            
            _logger.LogInformation("Balance check passed for Employee {EmployeeId}. Available: {Available}, Pending: {Pending}, Net: {Net}, Requested: {Requested}",
                employeeId, balance.RemainingDays, pendingDuration, balance.RemainingDays - pendingDuration, duration);
        }

        return Result.Success();
    }

    public async Task OnFinalApprovalAsync(Request request, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(request.Data);
        var data = doc.RootElement;
        
        var isHourly = data.TryGetProperty("isHourly", out var ih) && ih.GetBoolean();
        if (!isHourly)
        {
            var leaveSubType = (LeaveType)data.GetProperty("leaveSubType").GetInt32();
            var startDateTime = data.GetProperty("startDateTime").GetDateTime();
            var duration = data.GetProperty("duration").GetDecimal();

            var balance = await _unitOfWork.LeaveBalances.GetAsync(request.EmployeeId, leaveSubType, startDateTime.Year, ct);
            if (balance != null)
            {
                balance.UsedDays += duration;
            }
        }
    }
}
