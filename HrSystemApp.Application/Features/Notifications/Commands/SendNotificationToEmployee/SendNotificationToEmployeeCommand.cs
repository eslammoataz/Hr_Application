using System.Diagnostics;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly LoggingOptions _loggingOptions;

    public SendNotificationToEmployeeCommandHandler(
        INotificationService notificationService,
        ILogger<SendNotificationToEmployeeCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _notificationService = notificationService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result> Handle(SendNotificationToEmployeeCommand request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.Notifications.SendToEmployee);

        try
        {
            await _notificationService.SendNotificationAsync(
                request.EmployeeId,
                request.Title,
                request.Message,
                request.Type,
                cancellationToken);

            sw.Stop();
            _logger.LogActionSuccess(_loggingOptions, LogAction.Notifications.SendToEmployee, sw.ElapsedMilliseconds);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogActionFailure(_loggingOptions, LogAction.Notifications.SendToEmployee, LogStage.Processing, ex,
                new { EmployeeId = request.EmployeeId, NotificationType = request.Type.ToString() });
            sw.Stop();
            return Result.Failure(Errors.DomainErrors.Notification.SendFailed);
        }
    }
}
