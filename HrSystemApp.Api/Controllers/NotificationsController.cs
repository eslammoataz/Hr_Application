using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.DTOs.Notifications;
using HrSystemApp.Application.Features.Notifications.Commands.BroadcastNotification;
using HrSystemApp.Application.Features.Notifications.Commands.MarkNotificationAsRead;
using HrSystemApp.Application.Features.Notifications.Commands.SendNotificationToEmployee;
using HrSystemApp.Application.Features.Notifications.Queries.GetMyNotifications;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class NotificationsController : BaseApiController
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(List<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyNotifications(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMyNotificationsQuery(), cancellationToken);
        return HandleResult(result);
    }

    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new MarkNotificationAsReadCommand(id), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("send")]
    [Authorize(Roles = Roles.HrOrAbove)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SendToEmployee([FromBody] SendNotificationRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new SendNotificationToEmployeeCommand(request.EmployeeId, request.Title, request.Message, request.Type),
            cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("broadcast")]
    [Authorize(Roles = Roles.HrOrAbove)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastNotificationRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new BroadcastNotificationCommand(request.Title, request.Message, request.Type),
            cancellationToken);
        return HandleResult(result);
    }
}
