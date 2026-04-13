using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Auth.Commands.ForceChangePassword;

public class ForceChangePasswordCommandValidator : AbstractValidator<ForceChangePasswordCommand>
{
    public ForceChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage(Messages.Validation.CurrentPasswordRequired);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage(Messages.Validation.PasswordRequired)
            .MinimumLength(6).WithMessage(Messages.Validation.PasswordMinLength)
            .NotEqual(x => x.CurrentPassword).WithMessage(Messages.Validation.NewPasswordDifferent);
    }
}
