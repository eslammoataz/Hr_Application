using System.Diagnostics;
using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Application.Features.Requests.Strategies;

public class LeaveRequestStrategy : IRequestBusinessStrategy
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LeaveRequestStrategy> _logger;
    private readonly LoggingOptions _loggingOptions;

    public LeaveRequestStrategy(
        IUnitOfWork unitOfWork,
        ILogger<LeaveRequestStrategy> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
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

    public string TypeKey => "Leave";

    public async Task<Result> ValidateBusinessRulesAsync(Guid employeeId, Guid requestTypeId, JsonElement data, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.Workflow.LeaveValidation);

        var leaveSubType = (LeaveType)data.GetProperty("leaveSubType").GetInt32();
        var startDateTime = data.GetProperty("startDateTime").GetDateTime();
        var duration = data.GetProperty("duration").GetDecimal();
        var isHourly = data.TryGetProperty("isHourly", out var ih) && ih.GetBoolean();

        if (duration <= 0)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.LeaveValidation, LogStage.Validation,
                "InvalidDuration", new { EmployeeId = employeeId, Duration = duration });
            sw.Stop();
            return Result.Failure(DomainErrors.Requests.InvalidDuration);
        }

        if (!isHourly && IsPaidLeaveType(leaveSubType))
        {
            var balanceKey = GetBalanceKey(leaveSubType);

            var balance = await _unitOfWork.LeaveBalances.GetAsync(employeeId, balanceKey, startDateTime.Year, ct);

            if (balance == null)
            {
                _logger.LogDecision(_loggingOptions, LogAction.Workflow.LeaveValidation, LogStage.Validation,
                    "BalanceNotFound", new { EmployeeId = employeeId, BalanceKey = balanceKey.ToString(), Year = startDateTime.Year });
                sw.Stop();
                return Result.Failure(DomainErrors.LeaveBalance.NotFound);
            }

            var allPendingLeave = await _unitOfWork.Requests.FindAsync(r =>
                r.EmployeeId == employeeId &&
                r.RequestTypeId == requestTypeId &&
                (r.Status == RequestStatus.Submitted || r.Status == RequestStatus.InProgress), ct);

            decimal pendingDuration = 0;
            foreach (var req in allPendingLeave)
            {
                using var doc = JsonDocument.Parse(req.DynamicDataJson);
                var reqData = doc.RootElement;
                var reqIsHourly = reqData.TryGetProperty("isHourly", out var rih) && rih.GetBoolean();
                if (!reqIsHourly)
                    pendingDuration += reqData.GetProperty("duration").GetDecimal();
            }

            if (duration > balance.RemainingDays - pendingDuration)
            {
                _logger.LogDecision(_loggingOptions, LogAction.Workflow.LeaveValidation, LogStage.Validation,
                    "InsufficientBalance", new { EmployeeId = employeeId, Available = balance.RemainingDays - pendingDuration, Requested = duration });
                sw.Stop();
                return Result.Failure(DomainErrors.LeaveBalance.Insufficient);
            }
        }

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.Workflow.LeaveValidation, sw.ElapsedMilliseconds);

        return Result.Success();
    }

    public Task OnFinalApprovalAsync(Request request, CancellationToken ct)
        => Task.CompletedTask;
}
