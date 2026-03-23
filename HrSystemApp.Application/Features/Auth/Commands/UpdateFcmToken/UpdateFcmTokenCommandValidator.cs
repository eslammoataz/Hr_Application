using FluentValidation;

namespace HrSystemApp.Application.Features.Auth.Commands.UpdateFcmToken;

public class UpdateFcmTokenCommandValidator : AbstractValidator<UpdateFcmTokenCommand>
{
    public UpdateFcmTokenCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.FcmToken)
            .NotEmpty().WithMessage("FCM token is required.");

        RuleFor(x => x.DeviceType)
            .IsInEnum().WithMessage("Invalid device type specified.");
    }
}
