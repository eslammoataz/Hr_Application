using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Services;

public interface IFcmClient
{
    Task SendAsync(string token, Notification notification, NotificationType type, CancellationToken cancellationToken = default);
}
