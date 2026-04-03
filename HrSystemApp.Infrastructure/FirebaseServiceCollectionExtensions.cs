using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using HrSystemApp.Application.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Infrastructure;

public static class FirebaseServiceCollectionExtensions
{
    public static IServiceCollection AddFirebaseServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("FirebaseConfiguration");
            var environment = sp.GetRequiredService<IHostEnvironment>();
            var settings = sp.GetRequiredService<IOptions<FirebaseSettings>>().Value;

            if (string.IsNullOrWhiteSpace(settings.CredentialPath))
            {
                throw new InvalidOperationException(
                    $"{FirebaseSettings.SectionName}:CredentialPath must be configured for Firebase notifications.");
            }

            if (string.IsNullOrWhiteSpace(settings.AppName))
            {
                throw new InvalidOperationException(
                    $"{FirebaseSettings.SectionName}:AppName must be configured for Firebase notifications.");
            }

            if (string.IsNullOrWhiteSpace(settings.MessagingScope))
            {
                throw new InvalidOperationException(
                    $"{FirebaseSettings.SectionName}:MessagingScope must be configured for Firebase notifications.");
            }

            var resolvedPath = ResolveCredentialPath(settings.CredentialPath, environment.ContentRootPath);

            if (!File.Exists(resolvedPath))
            {
                throw new FileNotFoundException(
                    $"Firebase credential file was not found at '{resolvedPath}'.",
                    resolvedPath);
            }

            var existingApp = FirebaseApp.GetInstance(settings.AppName);
            if (existingApp is not null)
            {
                logger.LogInformation("Reusing Firebase app '{AppName}'.", existingApp.Name);
                return existingApp;
            }

            logger.LogInformation("Initializing Firebase app '{AppName}' using credentials from {Path}.",
                settings.AppName, resolvedPath);

            var firebaseApp = FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile(resolvedPath)
                    .CreateScoped(settings.MessagingScope)
            }, settings.AppName);

            logger.LogInformation("Firebase app '{AppName}' initialized successfully.", firebaseApp.Name);

            return firebaseApp;
        });

        services.AddSingleton(sp =>
        {
            var app = sp.GetRequiredService<FirebaseApp>();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("FirebaseConfiguration");
            var messaging = FirebaseMessaging.GetMessaging(app);

            logger.LogInformation(
                "Firebase messaging client created successfully for app '{AppName}'. Firebase is ready for notifications.",
                app.Name);

            return messaging;
        });

        services.AddHostedService<FirebaseStartupValidationService>();

        return services;
    }

    private static string ResolveCredentialPath(string credentialPath, string contentRootPath)
    {
        if (Path.IsPathRooted(credentialPath))
        {
            return credentialPath;
        }

        var candidatePaths = new[]
        {
            Path.GetFullPath(Path.Combine(contentRootPath, credentialPath)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, credentialPath)),
            Path.GetFullPath(Path.Combine(Directory.GetParent(contentRootPath)?.FullName ?? contentRootPath,
                credentialPath))
        };

        return candidatePaths.FirstOrDefault(File.Exists) ?? candidatePaths[0];
    }
}

internal sealed class FirebaseStartupValidationService : IHostedService
{
    private readonly FirebaseApp _firebaseApp;
    private readonly FirebaseMessaging _firebaseMessaging;
    private readonly ILogger<FirebaseStartupValidationService> _logger;

    public FirebaseStartupValidationService(
        FirebaseApp firebaseApp,
        FirebaseMessaging firebaseMessaging,
        ILogger<FirebaseStartupValidationService> logger)
    {
        _firebaseApp = firebaseApp;
        _firebaseMessaging = firebaseMessaging;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Firebase startup validation completed for app '{AppName}' with messaging client '{ClientType}'. Firebase connection is configured and notification sending is ready.",
            _firebaseApp.Name,
            _firebaseMessaging.GetType().Name);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
