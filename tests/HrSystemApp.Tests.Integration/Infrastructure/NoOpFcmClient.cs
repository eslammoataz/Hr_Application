using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Tests.Integration.Infrastructure;

public sealed class NoOpFcmClient : IFcmClient
{
    public Task SendAsync(string token, Notification notification, NotificationType type, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
