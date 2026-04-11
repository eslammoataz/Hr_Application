using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Services;

public interface INotificationService
{
    /// <summary>
/// Send a targeted notification to the specified employee.
/// </summary>
/// <param name="employeeId">The identifier of the employee who will receive the notification.</param>
/// <param name="title">A short title for the notification.</param>
/// <param name="message">The notification body text.</param>
/// <param name="type">The notification category or severity.</param>
/// <param name="cancellationToken">Optional token to cancel the operation.</param>
Task SendNotificationAsync(Guid employeeId, string title, string message, NotificationType type, CancellationToken cancellationToken = default);
    /// <summary>
/// Sends a broadcast notification to multiple recipients with the specified title, message, and notification type.
/// </summary>
/// <param name="title">The notification title.</param>
/// <param name="message">The notification body text.</param>
/// <param name="type">The category or severity of the notification.</param>
/// <param name="cancellationToken">A token to cancel the operation.</param>
/// <returns>A Task that completes when the broadcast operation finishes.</returns>
Task SendBroadcastAsync(string title, string message, NotificationType type, CancellationToken cancellationToken = default);
    /// <summary>
/// Retrieves notifications for a specific employee.
/// </summary>
/// <param name="employeeId">The unique identifier of the employee whose notifications are requested.</param>
/// <returns>An enumerable of Notification objects belonging to the specified employee.</returns>
Task<IEnumerable<Notification>> GetUserNotifications(Guid employeeId, CancellationToken cancellationToken = default);
    /// <summary>
/// Marks the specified notification as read for the given employee.
/// </summary>
/// <param name="notificationId">The identifier of the notification to mark as read.</param>
/// <param name="employeeId">The identifier of the employee for whom the notification should be marked as read.</param>
/// <returns>An integer indicating the result of the operation (for example, number of affected records).</returns>
Task<int> MarkAsReadAsync(Guid notificationId, Guid employeeId, CancellationToken cancellationToken = default);
}
