using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainNotification = HrSystemApp.Domain.Models.Notification;

namespace HrSystemApp.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotificationService> _logger;
    private readonly FcmSender _fcmSender;

    public NotificationService(
        ApplicationDbContext context,
        ILogger<NotificationService> logger,
        FcmSender fcmSender)
    {
        _context = context;
        _logger = logger;
        _fcmSender = fcmSender;
    }

    public async Task SendNotificationAsync(Guid employeeId, string title, string message, NotificationType type)
    {
        var employee = await _context.Employees
            .AsNoTracking()
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee is null)
        {
            _logger.LogWarning("Notification send failed. Employee {EmployeeId} not found", employeeId);
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

        await _context.Notifications.AddAsync(notification);
        await _context.SaveChangesAsync();

        if (employee.User is not null && !string.IsNullOrWhiteSpace(employee.User.FcmToken))
        {
            _ = Task.Run(() => _fcmSender.SendAsync(employee.User.FcmToken, notification, type));
        }
    }

    public async Task SendBroadcastAsync(string title, string message, NotificationType type)
    {
        var activeEmployees = await _context.Employees
            .AsNoTracking()
            .Include(e => e.User)
            .Where(e => e.EmploymentStatus == EmploymentStatus.Active && e.User != null && e.User.FcmToken != null)
            .ToListAsync();

        if (!activeEmployees.Any())
        {
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

        await _context.Notifications.AddRangeAsync(notifications);
        await _context.SaveChangesAsync();

        // created for O(1) lookups
        var notificationByEmployeeId = notifications.ToDictionary(n => n.EmployeeId);

        var batch = activeEmployees
            .Where(e => e.User?.FcmToken != null)
            .Select(e => (
                Token: e.User!.FcmToken!,
                Notification: notificationByEmployeeId[e.Id],
                Type: type
            ))
            .ToList();

        await _fcmSender.SendBatchAsync(batch);

        _logger.LogInformation("Broadcast notification {Title} added for {Count} employees", title,
            activeEmployees.Count);
    }

    public async Task<IEnumerable<DomainNotification>> GetUserNotifications(Guid employeeId)
    {
        return await _context.Notifications
            .AsNoTracking()
            .Where(n => n.EmployeeId == employeeId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> MarkAsReadAsync(Guid notificationId, Guid employeeId)
    {
        var rowsAffected = await _context.Notifications
            .Where(n => n.Id == notificationId && n.EmployeeId == employeeId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        if (rowsAffected > 0)
        {
            _logger.LogInformation("Notification {NotificationId} marked as read", notificationId);
        }

        return rowsAffected;
    }
}
