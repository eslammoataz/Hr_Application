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

    public FcmSender(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task SendAsync(string token, DomainNotification notification, NotificationType type)
    {
        // We use a separate logger here because we are outside the scope of the Task.Run when starting
        // But the Task itself will create its own scope and logger.
        
        return Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var fcmClient = scope.ServiceProvider.GetRequiredService<IFcmClient>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<FcmSender>>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            logger.LogInformation("Starting FCM notification {NotificationId} delivery", notification.Id);

            try
            {
                await fcmClient.SendAsync(token, notification, type, CancellationToken.None);
                logger.LogInformation("FCM notification {NotificationId} sent", notification.Id);
            }
            catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                                                        ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
            {
                logger.LogWarning("FCM token is invalid/unregistered. Clearing token. Error: {Error}",
                    ex.MessagingErrorCode);
                await ClearInvalidTokenAsync(context, token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "FCM delivery failed for notification {NotificationId}", notification.Id);
            }
        });
    }

    public Task SendBatchAsync(
        IEnumerable<(string Token, DomainNotification Notification, NotificationType Type)> batch)
    {
        foreach (var item in batch)
        {
            _ = SendAsync(item.Token, item.Notification, item.Type);
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