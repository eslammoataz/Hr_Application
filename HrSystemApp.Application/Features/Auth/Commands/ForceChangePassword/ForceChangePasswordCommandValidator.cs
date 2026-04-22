using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Auth.Commands.ForceChangePassword;

public class ForceChangePasswordCommandValidator : AbstractValidator<ForceChangePasswordCommand>
{
    public ForceChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithErrorCode(ErrorCodes.FieldRequired).WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithErrorCode(ErrorCodes.CurrentPasswordRequired).WithMessage(Messages.Validation.CurrentPasswordRequired);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithErrorCode(ErrorCodes.PasswordRequired).WithMessage(Messages.Validation.PasswordRequired)
            .MinimumLength(6).WithErrorCode(ErrorCodes.PasswordMinLength).WithMessage(Messages.Validation.PasswordMinLength)
            .NotEqual(x => x.CurrentPassword).WithErrorCode(ErrorCodes.NewPasswordDifferent).WithMessage(Messages.Validation.NewPasswordDifferent);
    }
}
