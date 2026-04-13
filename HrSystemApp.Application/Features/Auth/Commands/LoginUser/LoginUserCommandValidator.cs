using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Auth.Commands.LoginUser;

public class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(Messages.Validation.EmailRequired)
            .EmailAddress().WithMessage(Messages.Validation.ValidEmailRequired);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(Messages.Validation.PasswordRequired);

        RuleFor(x => x.FcmToken)
            .NotEmpty().When(x => x.FcmToken != null).WithMessage(Messages.Validation.FcmTokenNotEmpty);

        RuleFor(x => x.DeviceType)
            .IsInEnum().When(x => x.DeviceType != null).WithMessage(Messages.Validation.InvalidDeviceType);

        RuleFor(x => x.Language)
            .NotEmpty().When(x => x.Language != null).WithMessage(Messages.Validation.LanguageNotEmpty)
            .MaximumLength(10).When(x => x.Language != null).WithMessage(Messages.Validation.LanguageMaxLength);
    }
}
