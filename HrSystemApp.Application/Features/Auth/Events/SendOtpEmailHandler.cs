using MediatR;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Common.Events;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Auth.Events;

public class SendOtpEmailHandler : INotificationHandler<OtpGeneratedEvent>
{
    private readonly IEmailService _emailService;

    public SendOtpEmailHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task Handle(OtpGeneratedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.Channel != OtpChannel.Email) return;

        await _emailService.SendOtpAsync(notification.Email, notification.Otp, cancellationToken);
    }
}
