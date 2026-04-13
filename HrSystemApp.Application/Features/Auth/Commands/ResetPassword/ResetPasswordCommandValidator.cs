using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Auth.Commands.ResetPassword;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(Messages.Validation.EmailRequired)
            .EmailAddress().WithMessage(Messages.Validation.ValidEmailRequired);

        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage(Messages.Validation.OtpRequired)
            .Length(6).WithMessage(Messages.Validation.OtpMustBe6Chars)
            .Matches("^[0-9]{6}$").WithMessage(Messages.Validation.OtpMustBeNumeric);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage(Messages.Validation.PasswordRequired)
            .MinimumLength(8).WithMessage(Messages.Validation.PasswordMinLength8);
    }
}
