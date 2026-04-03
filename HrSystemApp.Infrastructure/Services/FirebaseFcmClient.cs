using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Infrastructure.Services;

public class FirebaseFcmClient : IFcmClient
{
    private static readonly object SyncLock = new();
    private static FirebaseApp? _firebaseApp;

    private readonly ILogger<FirebaseFcmClient> _logger;
    private readonly string? _credentialPath;

    public FirebaseFcmClient(ILogger<FirebaseFcmClient> logger, IConfiguration configuration)
    {
        _logger = logger;
        _credentialPath = configuration["Firebase:CredentialPath"];
        EnsureFirebaseApp();
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
                ["notificationId"] = notification.Id.ToString(),
                ["employeeId"] = notification.EmployeeId.ToString(),
                ["type"] = type.ToString()
            }
        };

        try
        {
            _logger.LogInformation("Attempting to send FCM notification {NotificationId} to token {Token}", 
                notification.Id, token[..10] + "...");

            var response = await FirebaseMessaging.GetMessaging(_firebaseApp!).SendAsync(message, cancellationToken);
            
            _logger.LogInformation("FCM notification {NotificationId} sent successfully. Response: {Response}", 
                notification.Id, response);
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

    private void EnsureFirebaseApp()
    {
        if (_firebaseApp is not null)
        {
            return;
        }

        lock (SyncLock)
        {
            if (_firebaseApp is not null)
            {
                return;
            }

            var credential = GetCredential();

            _firebaseApp = FirebaseApp.Create(new AppOptions
            {
                Credential = credential
            }, "HrSystemAppNotifications");

            _logger.LogInformation("Firebase app '{AppName}' initialized for FCM notifications.", _firebaseApp.Name);
        }
    }

    private GoogleCredential GetCredential()
    {
        if (!string.IsNullOrWhiteSpace(_credentialPath))
        {
            _logger.LogInformation("Using Firebase credentials from config path: {Path}", _credentialPath);
            return GoogleCredential.FromFile(_credentialPath)
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
        }

        var envPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            _logger.LogInformation("Using Firebase credentials from env var: {Path}", envPath);
            return GoogleCredential.FromFile(envPath)
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
        }

        _logger.LogWarning("Firebase credential not configured. Attempting Application Default Credentials.");
        return GoogleCredential.GetApplicationDefault()
            .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
    }
}
