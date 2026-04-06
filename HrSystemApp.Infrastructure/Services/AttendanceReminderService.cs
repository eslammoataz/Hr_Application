using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Infrastructure.Services;

public class AttendanceReminderService : IAttendanceReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttendanceRulesProvider _attendanceRulesProvider;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AttendanceReminderService> _logger;

    public AttendanceReminderService(
        IUnitOfWork unitOfWork,
        IAttendanceRulesProvider attendanceRulesProvider,
        INotificationService notificationService,
        ILogger<AttendanceReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _attendanceRulesProvider = attendanceRulesProvider;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task ProcessRemindersAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var candidates = await _unitOfWork.Attendances.GetIncompleteAttendancesAsync(nowUtc, cancellationToken);

        foreach (var attendance in candidates)
        {
            if (attendance.LastClockOutUtc is not null)
            {
                continue;
            }

            var rules = await _attendanceRulesProvider.GetRulesAsync(attendance.EmployeeId, attendance.Date, cancellationToken);
            if (nowUtc < rules.ReminderDueUtc)
            {
                continue;
            }

            var windowKey = $"{attendance.Date:yyyyMMdd}:{rules.ReminderDueUtc:yyyyMMddHHmm}";
            var exists = await _unitOfWork.AttendanceReminderLogs.ExistsForWindowAsync(
                attendance.Id,
                AttendanceReminderType.MissingClockOut,
                windowKey,
                cancellationToken);

            if (exists)
            {
                continue;
            }

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
                _logger.LogError(ex, "Failed sending attendance reminder for employee {EmployeeId}", attendance.EmployeeId);
            }

            await _unitOfWork.AttendanceReminderLogs.AddAsync(reminderLog, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
