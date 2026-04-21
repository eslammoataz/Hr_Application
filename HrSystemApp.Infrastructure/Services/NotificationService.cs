using System.Diagnostics;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common.Logging;
using DomainNotification = HrSystemApp.Domain.Models.Notification;

namespace HrSystemApp.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotificationService> _logger;
    private readonly FcmSender _fcmSender;
    private readonly LoggingOptions _loggingOptions;

    public NotificationService(
        ApplicationDbContext context,
        ILogger<NotificationService> logger,
        FcmSender fcmSender,
        IOptions<LoggingOptions> loggingOptions)
    {
        _context = context;
        _logger = logger;
        _fcmSender = fcmSender;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task SendNotificationAsync(Guid employeeId, string title, string message, NotificationType type, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.Attendance.AttendanceReminder);

        var employee = await _context.Employees
            .AsNoTracking()
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

        if (employee is null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Attendance.AttendanceReminder, LogStage.Processing,
                "EmployeeNotFound", new { EmployeeId = employeeId });
            sw.Stop();
            throw new KeyNotFoundException(DomainErrors.Employee.NotFound.Message);
        }

        var notification = new DomainNotification
        {
            EmployeeId = employeeId,
            Title = title,
            Message = message,
            Type = type,
            IsRead = false
        };

        await _context.Notifications.AddAsync(notification, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        if (employee.User is not null && !string.IsNullOrWhiteSpace(employee.User.FcmToken))
        {
            _ = _fcmSender.SendAsync(employee.User.FcmToken, notification, type, cancellationToken);
            sw.Stop();
            _logger.LogActionSuccess(_loggingOptions, LogAction.Attendance.AttendanceReminder, sw.ElapsedMilliseconds);
            return;
        }

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.Attendance.AttendanceReminder, sw.ElapsedMilliseconds);
    }

    public async Task SendBroadcastAsync(string title, string message, NotificationType type, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.Attendance.AttendanceReminder);

        var activeEmployees = await _context.Employees
            .AsNoTracking()
            .Include(e => e.User)
            .Where(e => e.EmploymentStatus == EmploymentStatus.Active && e.User != null && e.User.FcmToken != null)
            .ToListAsync(cancellationToken);

        if (!activeEmployees.Any())
        {
            _logger.LogDecision(_loggingOptions, LogAction.Attendance.AttendanceReminder, LogStage.Processing,
                "NoActiveRecipients", new { });
            sw.Stop();
            _logger.LogActionSuccess(_loggingOptions, LogAction.Attendance.AttendanceReminder, sw.ElapsedMilliseconds);
            return;
        }

        var notifications = activeEmployees.Select(e => new DomainNotification
        {
            EmployeeId = e.Id,
            Title = title,
            Message = message,
            Type = type,
            IsRead = false
        }).ToList();

        await _context.Notifications.AddRangeAsync(notifications, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var notificationByEmployeeId = notifications.ToDictionary(n => n.EmployeeId);

        var batch = activeEmployees
            .Where(e => e.User?.FcmToken != null)
            .Select(e => (
                Token: e.User!.FcmToken!,
                Notification: notificationByEmployeeId[e.Id],
                Type: type
            ))
            .ToList();

        await _fcmSender.SendBatchAsync(batch, cancellationToken);

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.Attendance.AttendanceReminder, sw.ElapsedMilliseconds);
    }

    public async Task<IEnumerable<DomainNotification>> GetUserNotifications(Guid employeeId, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .AsNoTracking()
            .Where(n => n.EmployeeId == employeeId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> MarkAsReadAsync(Guid notificationId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await _context.Notifications
            .Where(n => n.Id == notificationId && n.EmployeeId == employeeId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancellationToken);

        return rowsAffected;
    }
}