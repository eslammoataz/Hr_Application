using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Services;

public interface INotificationService
{
    Task SendNotificationAsync(Guid employeeId, string title, string message, NotificationType type);
    Task SendBroadcastAsync(string title, string message, NotificationType type);
    Task<IEnumerable<Notification>> GetUserNotifications(Guid employeeId);
    Task<int> MarkAsReadAsync(Guid notificationId, Guid employeeId);
}
