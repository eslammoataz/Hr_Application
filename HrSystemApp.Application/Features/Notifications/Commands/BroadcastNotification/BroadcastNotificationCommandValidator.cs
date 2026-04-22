using FluentValidation;
using HrSystemApp.Application.Resources;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Notifications.Commands.BroadcastNotification;

public class BroadcastNotificationCommandValidator : AbstractValidator<BroadcastNotificationCommand>
{
    public BroadcastNotificationCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithErrorCode(ErrorCodes.TitleRequired).WithMessage(Messages.Validation.TitleRequired)
            .MaximumLength(200).WithErrorCode(ErrorCodes.TitleMaxLength).WithMessage(Messages.Validation.TitleMaxLength);

        RuleFor(x => x.Message)
            .NotEmpty().WithErrorCode(ErrorCodes.MessageRequired).WithMessage(Messages.Validation.MessageRequired)
            .MaximumLength(2000).WithErrorCode(ErrorCodes.MessageMaxLength).WithMessage(Messages.Validation.MessageMaxLength);

        RuleFor(x => x.Type)
            .IsInEnum().WithErrorCode(ErrorCodes.InvalidNotificationType).WithMessage(Messages.Validation.InvalidNotificationType);
    }
}
