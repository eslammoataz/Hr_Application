using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.Notifications;

public record NotificationResponse(
    Guid Id,
    Guid EmployeeId,
    string Title,
    string Message,
    NotificationType Type,
    bool IsRead,
    DateTime CreatedAt);

public record SendNotificationRequest(
    Guid EmployeeId,
    string Title,
    string Message,
    NotificationType Type);

public record BroadcastNotificationRequest(
    string Title,
    string Message,
    NotificationType Type);
