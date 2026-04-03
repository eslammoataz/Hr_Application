using HrSystemApp.Application.Common;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.Notifications.Commands.SendNotificationToEmployee;

public record SendNotificationToEmployeeCommand(
    Guid EmployeeId,
    string Title,
    string Message,
    NotificationType Type) : IRequest<Result>;

public class SendNotificationToEmployeeCommandHandler : IRequestHandler<SendNotificationToEmployeeCommand, Result>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<SendNotificationToEmployeeCommandHandler> _logger;

    public SendNotificationToEmployeeCommandHandler(
        INotificationService notificationService,
        ILogger<SendNotificationToEmployeeCommandHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Result> Handle(SendNotificationToEmployeeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _notificationService.SendNotificationAsync(
                request.EmployeeId,
                request.Title,
                request.Message,
                request.Type);

            _logger.LogInformation("Notification {Type} sent to employee {EmployeeId}", request.Type,
                request.EmployeeId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification {Type} to employee {EmployeeId}", request.Type,
                request.EmployeeId);
            return Result.Failure(Errors.DomainErrors.Notification.SendFailed);
        }
    }
}
