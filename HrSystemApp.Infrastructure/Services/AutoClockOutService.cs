using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Features.Attendance.Common;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Infrastructure.Services;

public class AutoClockOutService : IAutoClockOutService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttendanceRulesProvider _attendanceRulesProvider;
    private readonly ILogger<AutoClockOutService> _logger;

    public AutoClockOutService(
        IUnitOfWork unitOfWork,
        IAttendanceRulesProvider attendanceRulesProvider,
        ILogger<AutoClockOutService> logger)
    {
        _unitOfWork = unitOfWork;
        _attendanceRulesProvider = attendanceRulesProvider;
        _logger = logger;
    }

    public async Task ProcessAutoClockOutAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var candidates = await _unitOfWork.Attendances.GetIncompleteAttendancesAsync(nowUtc, cancellationToken);

        foreach (var attendance in candidates)
        {
            if (attendance.FirstClockInUtc is null || attendance.LastClockOutUtc is not null)
            {
                continue;
            }

            var rules = await _attendanceRulesProvider.GetRulesAsync(attendance.EmployeeId, attendance.Date, cancellationToken);
            if (rules.ShiftEndUtc > nowUtc)
            {
                continue;
            }

            var idempotencyKey = $"AUTO:{attendance.Id}:{rules.ShiftEndUtc:yyyyMMddHHmmss}";
            if (await _unitOfWork.AttendanceLogs.ExistsByIdempotencyKeyAsync(idempotencyKey, cancellationToken))
            {
                continue;
            }

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
                AttendanceSummaryCalculator.ApplyClockOut(attendance, rules.ShiftEndUtc, rules.ShiftEndUtc, "Auto Clock-Out");
                await _unitOfWork.Attendances.UpdateAsync(attendance, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                _logger.LogError(ex, "Auto clock-out failed for attendance {AttendanceId}", attendance.Id);
            }
        }
    }
}
