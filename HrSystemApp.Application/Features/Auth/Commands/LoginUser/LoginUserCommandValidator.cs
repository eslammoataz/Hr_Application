using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Auth.Commands.LoginUser;

public class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode(ErrorCodes.EmailRequired).WithMessage(Messages.Validation.EmailRequired)
            .EmailAddress().WithErrorCode(ErrorCodes.ValidEmailRequired).WithMessage(Messages.Validation.ValidEmailRequired);

        RuleFor(x => x.Password)
            .NotEmpty().WithErrorCode(ErrorCodes.PasswordRequired).WithMessage(Messages.Validation.PasswordRequired);

        RuleFor(x => x.FcmToken)
            .NotEmpty().When(x => x.FcmToken != null).WithErrorCode(ErrorCodes.FcmTokenNotEmpty).WithMessage(Messages.Validation.FcmTokenNotEmpty);

        RuleFor(x => x.DeviceType)
            .IsInEnum().When(x => x.DeviceType != null).WithErrorCode(ErrorCodes.InvalidDeviceType).WithMessage(Messages.Validation.InvalidDeviceType);

        RuleFor(x => x.Language)
            .NotEmpty().When(x => x.Language != null).WithErrorCode(ErrorCodes.LanguageNotEmpty).WithMessage(Messages.Validation.LanguageNotEmpty)
            .MaximumLength(10).When(x => x.Language != null).WithErrorCode(ErrorCodes.LanguageMaxLength).WithMessage(Messages.Validation.LanguageMaxLength);
    }
}
