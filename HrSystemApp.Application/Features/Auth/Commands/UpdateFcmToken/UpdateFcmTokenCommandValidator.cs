using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Auth.Commands.UpdateFcmToken;

public class UpdateFcmTokenCommandValidator : AbstractValidator<UpdateFcmTokenCommand>
{
    public UpdateFcmTokenCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.FcmToken)
            .NotEmpty().WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.DeviceType)
            .IsInEnum().WithMessage(Messages.Validation.InvalidDeviceType);
    }
}
