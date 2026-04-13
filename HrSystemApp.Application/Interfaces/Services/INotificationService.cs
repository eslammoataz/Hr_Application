using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Services;

public interface INotificationService
{
    Task SendNotificationAsync(Guid employeeId, string title, string message, NotificationType type, CancellationToken cancellationToken = default);
    Task SendBroadcastAsync(string title, string message, NotificationType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<Notification>> GetUserNotifications(Guid employeeId, CancellationToken cancellationToken = default);
    Task<int> MarkAsReadAsync(Guid notificationId, Guid employeeId, CancellationToken cancellationToken = default);
}
