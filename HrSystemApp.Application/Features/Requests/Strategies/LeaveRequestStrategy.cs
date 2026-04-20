using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
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

    private static readonly HashSet<LeaveType> PaidLeaveTypes = new()
    {
        LeaveType.Annual,
        LeaveType.Emergency,
        LeaveType.Permission,
        LeaveType.Sick,
        LeaveType.Maternity,
        LeaveType.Other
    };

    private static bool IsPaidLeaveType(LeaveType type) => PaidLeaveTypes.Contains(type);

    private static LeaveType GetBalanceKey(LeaveType leaveSubType)
        => IsPaidLeaveType(leaveSubType) ? LeaveType.Annual : leaveSubType;

    public RequestType Type => RequestType.Leave;

    public async Task<Result> ValidateBusinessRulesAsync(Guid employeeId, JsonElement data, CancellationToken ct)
    {
        var leaveSubType = (LeaveType)data.GetProperty("leaveSubType").GetInt32();
        var startDateTime = data.GetProperty("startDateTime").GetDateTime();
        var duration = data.GetProperty("duration").GetDecimal();
        var isHourly = data.TryGetProperty("isHourly", out var ih) && ih.GetBoolean();

        _logger.LogInformation(
            "[LeaveValidation] EmployeeId={EmployeeId}, LeaveType={LeaveType}, Duration={Duration}, IsHourly={IsHourly}, Year={Year}",
            employeeId, leaveSubType, duration, isHourly, startDateTime.Year);

        if (duration <= 0)
        {
            _logger.LogWarning("[LeaveValidation] Invalid duration {Duration} for EmployeeId={EmployeeId}", duration, employeeId);
            return Result.Failure(DomainErrors.Requests.InvalidDuration);
        }

        if (!isHourly && IsPaidLeaveType(leaveSubType))
        {
            var balanceKey = GetBalanceKey(leaveSubType);
            _logger.LogInformation("[LeaveValidation] Paid leave — checking balance against {BalanceKey}", balanceKey);

            var balance = await _unitOfWork.LeaveBalances.GetAsync(employeeId, balanceKey, startDateTime.Year, ct);

            if (balance == null)
            {
                _logger.LogError(
                    "[LeaveValidation] BALANCE NOT FOUND — EmployeeId={EmployeeId}, BalanceKey={BalanceKey}, Year={Year}",
                    employeeId, balanceKey, startDateTime.Year);
                return Result.Failure(DomainErrors.LeaveBalance.NotFound);
            }

            _logger.LogInformation(
                "[LeaveValidation] Balance found — TotalDays={TotalDays}, UsedDays={UsedDays}, Remaining={Remaining}",
                balance.TotalDays, balance.UsedDays, balance.RemainingDays);

            var allPendingLeave = await _unitOfWork.Requests.FindAsync(r =>
                r.EmployeeId == employeeId &&
                r.RequestType == RequestType.Leave &&
                (r.Status == RequestStatus.Submitted || r.Status == RequestStatus.InProgress), ct);

            decimal pendingDuration = 0;
            foreach (var req in allPendingLeave)
            {
                using var doc = JsonDocument.Parse(req.Data);
                var reqData = doc.RootElement;
                var reqIsHourly = reqData.TryGetProperty("isHourly", out var rih) && rih.GetBoolean();
                if (!reqIsHourly)
                    pendingDuration += reqData.GetProperty("duration").GetDecimal();
            }

            _logger.LogInformation(
                "[LeaveValidation] Balance check — Available={Available}, Pending={Pending}, Net={Net}, Requested={Requested}",
                balance.RemainingDays, pendingDuration, balance.RemainingDays - pendingDuration, balance.RemainingDays);

            if (duration > balance.RemainingDays - pendingDuration)
            {
                _logger.LogWarning("[LeaveValidation] Insufficient balance — EmployeeId={EmployeeId}", employeeId);
                return Result.Failure(DomainErrors.LeaveBalance.Insufficient);
            }

            return Result.Success();
        }
        
        return Result.Success();
    }

    public Task OnFinalApprovalAsync(Request request, CancellationToken ct)
        => Task.CompletedTask;
}
