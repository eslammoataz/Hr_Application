using HrSystemApp.Application.Common;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.Notifications.Commands.BroadcastNotification;

public record BroadcastNotificationCommand(
    string Title,
    string Message,
    NotificationType Type) : IRequest<Result>;

public class BroadcastNotificationCommandHandler : IRequestHandler<BroadcastNotificationCommand, Result>
{
    private readonly INotificationService _notificationService;

    public BroadcastNotificationCommandHandler(
        INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<Result> Handle(BroadcastNotificationCommand request, CancellationToken cancellationToken)
    {
        await _notificationService.SendBroadcastAsync(
            request.Title,
            request.Message,
            request.Type);

        return Result.Success();
    }
}
