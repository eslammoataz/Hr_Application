using System.Diagnostics;
using HrSystemApp.Application.Features.Attendance.Common;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Infrastructure.Services;

public class AutoClockOutService : IAutoClockOutService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttendanceRulesProvider _attendanceRulesProvider;
    private readonly ILogger<AutoClockOutService> _logger;
    private readonly LoggingOptions _loggingOptions;

    public AutoClockOutService(
        IUnitOfWork unitOfWork,
        IAttendanceRulesProvider attendanceRulesProvider,
        ILogger<AutoClockOutService> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _attendanceRulesProvider = attendanceRulesProvider;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task ProcessAutoClockOutAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var runId = Guid.NewGuid();
        _logger.LogActionStart(_loggingOptions, LogAction.Attendance.AutoClockOut);

        var nowUtc = DateTime.UtcNow;
        var candidates = await _unitOfWork.Attendances.GetIncompleteAttendancesAsync(nowUtc, cancellationToken);
        var clockOutCount = 0;

        foreach (var attendance in candidates)
        {
            if (attendance.FirstClockInUtc is null || attendance.LastClockOutUtc is not null)
                continue;

            var rules = await _attendanceRulesProvider.GetRulesAsync(attendance.EmployeeId, attendance.Date, cancellationToken);
            if (rules.ShiftEndUtc > nowUtc)
                continue;

            var idempotencyKey = $"AUTO:{attendance.Id}:{rules.ShiftEndUtc:yyyyMMddHHmmss}";
            if (await _unitOfWork.AttendanceLogs.ExistsByIdempotencyKeyAsync(idempotencyKey, cancellationToken))
                continue;

            var lastClockIn = await _unitOfWork.AttendanceLogs.GetLastClockInAsync(attendance.Id, cancellationToken);
            var sessionStartUtc = lastClockIn?.TimestampUtc ?? attendance.FirstClockInUtc!.Value;

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var log = new AttendanceLog
                {
                    AttendanceId = attendance.Id,
                    EmployeeId = attendance.EmployeeId,
                    TimestampUtc = rules.ShiftEndUtc,
                    Type = AttendanceLogType.ClockOut,
                    Reason = "Auto Clock-Out",
                    Source = AttendanceLogSource.System,
                    CreatedAtUtc = DateTime.UtcNow,
                    IdempotencyKey = idempotencyKey
                };

                await _unitOfWork.AttendanceLogs.AddAsync(log, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                attendance.LastClockOutLogId = log.Id;
                AttendanceSummaryCalculator.ApplyClockOut(attendance, rules.ShiftEndUtc, rules.ShiftEndUtc, sessionStartUtc, "Auto Clock-Out");
                await _unitOfWork.Attendances.UpdateAsync(attendance, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                clockOutCount++;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                _logger.LogActionFailure(_loggingOptions, LogAction.Attendance.AutoClockOut, LogStage.Processing, ex,
                    new { AttendanceId = attendance.Id });
            }
        }

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.Attendance.AutoClockOut, sw.ElapsedMilliseconds);
    }
}