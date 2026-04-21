using System.Diagnostics;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Infrastructure.Services;

public class AttendanceReminderService : IAttendanceReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttendanceRulesProvider _attendanceRulesProvider;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AttendanceReminderService> _logger;
    private readonly LoggingOptions _loggingOptions;

    public AttendanceReminderService(
        IUnitOfWork unitOfWork,
        IAttendanceRulesProvider attendanceRulesProvider,
        INotificationService notificationService,
        ILogger<AttendanceReminderService> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _attendanceRulesProvider = attendanceRulesProvider;
        _notificationService = notificationService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task ProcessRemindersAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var runId = Guid.NewGuid();
        _logger.LogActionStart(_loggingOptions, LogAction.Attendance.AttendanceReminder);

        var nowUtc = DateTime.UtcNow;
        var candidates = await _unitOfWork.Attendances.GetIncompleteAttendancesAsync(nowUtc, cancellationToken);
        var employeeCount = 0;

        foreach (var attendance in candidates)
        {
            if (attendance.LastClockOutUtc is not null)
                continue;

            var rules = await _attendanceRulesProvider.GetRulesAsync(attendance.EmployeeId, attendance.Date, cancellationToken);
            if (nowUtc < rules.ReminderDueUtc)
                continue;

            var windowKey = $"{attendance.Date:yyyyMMdd}:{rules.ReminderDueUtc:yyyyMMddHHmm}";
            var exists = await _unitOfWork.AttendanceReminderLogs.ExistsForWindowAsync(
                attendance.Id,
                AttendanceReminderType.MissingClockOut,
                windowKey,
                cancellationToken);

            if (exists)
                continue;

            employeeCount++;

            var reminderLog = new AttendanceReminderLog
            {
                AttendanceId = attendance.Id,
                EmployeeId = attendance.EmployeeId,
                ReminderType = AttendanceReminderType.MissingClockOut,
                SentAtUtc = nowUtc,
                Status = AttendanceReminderStatus.Skipped,
                WindowKey = windowKey,
                Channel = "Notification"
            };

            try
            {
                await _notificationService.SendNotificationAsync(
                    attendance.EmployeeId,
                    "Clock-Out Reminder",
                    "You have not clocked out yet. Please clock out now.",
                    NotificationType.AttendanceReminder);

                reminderLog.Status = AttendanceReminderStatus.Sent;
            }
            catch (Exception ex)
            {
                reminderLog.Status = AttendanceReminderStatus.Failed;
                reminderLog.ErrorMessage = ex.Message;
            }

            await _unitOfWork.AttendanceReminderLogs.AddAsync(reminderLog, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.Attendance.AttendanceReminder, sw.ElapsedMilliseconds);
    }
}