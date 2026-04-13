using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Auth.Commands.ChangePassword;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage(Messages.Validation.CurrentPasswordRequired);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage(Messages.Validation.PasswordRequired)
            .MinimumLength(6).WithMessage(Messages.Validation.PasswordMinLength)
            .NotEqual(x => x.CurrentPassword).WithMessage(Messages.Validation.NewPasswordDifferent);
    }
}
