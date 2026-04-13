using FirebaseAdmin.Messaging;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Constants;
using HrSystemApp.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Infrastructure.Services;

public class FirebaseFcmClient : IFcmClient
{
    private const string NotificationIdDataKey = "notificationId";
    private readonly string EmployeeIdDataKey = AppClaimTypes.EmployeeId;
    private const string TypeDataKey = "type";

    private readonly FirebaseMessaging _firebaseMessaging;
    private readonly ILogger<FirebaseFcmClient> _logger;

    public FirebaseFcmClient(FirebaseMessaging firebaseMessaging, ILogger<FirebaseFcmClient> logger)
    {
        _firebaseMessaging = firebaseMessaging;
        _logger = logger;

        _logger.LogInformation(
            "FirebaseFcmClient initialized successfully with Firebase messaging client {ClientType}.",
            firebaseMessaging.GetType().Name);
    }

    public async Task SendAsync(string token, Domain.Models.Notification notification, NotificationType type,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Skipping FCM send because token is empty for notification {NotificationId}",
                notification.Id);
            throw new InvalidOperationException("FCM token is missing.");
        }

        var message = new Message
        {
            Token = token,
            Notification = new Notification
            {
                Title = notification.Title,
                Body = notification.Message
            },
            Data = new Dictionary<string, string>
            {
                [NotificationIdDataKey] = notification.Id.ToString(),
                [EmployeeIdDataKey] = notification.EmployeeId.ToString(),
                [TypeDataKey] = type.ToString()
            }
        };

        try
        {
            _logger.LogInformation(
                "Sending Firebase message for notification {NotificationId}, employee {EmployeeId}, type {Type} to token {Token}",
                notification.Id,
                notification.EmployeeId,
                type,
                token[..10] + "...");

            var response = await _firebaseMessaging.SendAsync(message, cancellationToken);

            _logger.LogInformation(
                "Firebase message for notification {NotificationId} sent successfully. Firebase response id: {Response}",
                notification.Id,
                response);
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogError(ex, "Firebase Messaging error sending notification {NotificationId}: {Error} - {Message}",
                notification.Id, ex.MessagingErrorCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending FCM notification {NotificationId}: {Message}",
                notification.Id, ex.Message);
            throw;
        }
    }
}
