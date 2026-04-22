using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Auth.Commands.UpdateFcmToken;

public class UpdateFcmTokenCommandValidator : AbstractValidator<UpdateFcmTokenCommand>
{
    public UpdateFcmTokenCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithErrorCode(ErrorCodes.FieldRequired).WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.FcmToken)
            .NotEmpty().WithErrorCode(ErrorCodes.FieldRequired).WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.DeviceType)
            .IsInEnum().WithErrorCode(ErrorCodes.InvalidDeviceType).WithMessage(Messages.Validation.InvalidDeviceType);
    }
}
