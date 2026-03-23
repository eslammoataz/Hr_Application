using MediatR;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Common.Events;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Auth.Events;

public class SendOtpSmsHandler : INotificationHandler<OtpGeneratedEvent>
{
    private readonly ISmsService _smsService;

    public SendOtpSmsHandler(ISmsService smsService)
    {
        _smsService = smsService;
    }

    public async Task Handle(OtpGeneratedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.Channel != OtpChannel.Sms || string.IsNullOrEmpty(notification.PhoneNumber)) return;

        await _smsService.SendOtpAsync(notification.PhoneNumber, notification.Otp, cancellationToken);
    }
}
