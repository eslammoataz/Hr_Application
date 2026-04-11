using FirebaseAdmin.Messaging;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DomainNotification = HrSystemApp.Domain.Models.Notification;

namespace HrSystemApp.Infrastructure.Services;

public class FcmSender
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FcmSender> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="FcmSender"/> configured to send Firebase notifications using scoped services.
    /// </summary>
    /// <param name="scopeFactory">Factory for creating DI scopes to resolve per-delivery scoped services.</param>
    /// <param name="logger">Logger used to record delivery queueing, start/end events, and errors.</param>
    public FcmSender(IServiceScopeFactory scopeFactory, ILogger<FcmSender> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Queues a Firebase Cloud Messaging delivery for the specified notification and recipient token.
    /// </summary>
    /// <param name="token">The recipient's FCM registration token.</param>
    /// <param name="notification">The notification payload to send.</param>
    /// <param name="type">The classification of the notification to send.</param>
    /// <param name="cancellationToken">Optional token forwarded to the send operation to cancel delivery.</param>
    /// <returns>A Task representing the queued background delivery operation.</returns>
    /// <remarks>
    /// If Firebase reports the token as unregistered or invalid, the sender clears that token from the database for the associated user.
    /// </remarks>
    public Task SendAsync(string token, DomainNotification notification, NotificationType type, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Queueing Firebase delivery task for notification {NotificationId}, employee {EmployeeId}, type {Type}.",
            notification.Id,
            notification.EmployeeId,
            type);

        return Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var fcmClient = scope.ServiceProvider.GetRequiredService<IFcmClient>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<FcmSender>>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            logger.LogInformation(
                "Starting Firebase delivery for notification {NotificationId} to employee {EmployeeId}.",
                notification.Id,
                notification.EmployeeId);

            try
            {
                await fcmClient.SendAsync(token, notification, type, cancellationToken);
                logger.LogInformation(
                    "Firebase delivery completed successfully for notification {NotificationId}.",
                    notification.Id);
            }
            catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                                                        ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
            {
                logger.LogWarning(
                    "Firebase rejected token for notification {NotificationId}. Token is invalid or unregistered. Clearing token. Error: {Error}",
                    notification.Id,
                    ex.MessagingErrorCode);
                await ClearInvalidTokenAsync(context, token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Firebase delivery failed for notification {NotificationId}", notification.Id);
            }
        });
    }

    /// <summary>
    /// Queues individual Firebase deliveries for a batch of notifications.
    /// </summary>
    /// <param name="batch">A collection of tuples containing the target FCM token, the notification to send, and the notification type.</param>
    /// <param name="cancellationToken">A token propagated to each send operation to allow cancellation of individual deliveries.</param>
    /// <returns>A <see cref="Task"/> that completes after all send operations have been started; it does not wait for delivery completion.</returns>
    public Task SendBatchAsync(
        IEnumerable<(string Token, DomainNotification Notification, NotificationType Type)> batch,
        CancellationToken cancellationToken = default)
    {
        var notifications = batch.ToList();

        _logger.LogInformation(
            "Queueing Firebase batch delivery for {Count} notifications.",
            notifications.Count);

        foreach (var item in notifications)
        {
            _ = SendAsync(item.Token, item.Notification, item.Type, cancellationToken);
        }

        return Task.CompletedTask;
    }

    private static async Task ClearInvalidTokenAsync(ApplicationDbContext context, string token)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.FcmToken == token);
        if (user is not null)
        {
            user.FcmToken = null;
            await context.SaveChangesAsync();
        }
    }
}
