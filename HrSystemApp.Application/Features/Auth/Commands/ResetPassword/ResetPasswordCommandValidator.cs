using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Auth.Commands.ResetPassword;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode(ErrorCodes.EmailRequired).WithMessage(Messages.Validation.EmailRequired)
            .EmailAddress().WithErrorCode(ErrorCodes.ValidEmailRequired).WithMessage(Messages.Validation.ValidEmailRequired);

        RuleFor(x => x.Otp)
            .NotEmpty().WithErrorCode(ErrorCodes.OtpRequired).WithMessage(Messages.Validation.OtpRequired)
            .Length(6).WithErrorCode(ErrorCodes.OtpMustBe6Chars).WithMessage(Messages.Validation.OtpMustBe6Chars)
            .Matches("^[0-9]{6}$").WithErrorCode(ErrorCodes.OtpMustBeNumeric).WithMessage(Messages.Validation.OtpMustBeNumeric);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithErrorCode(ErrorCodes.PasswordRequired).WithMessage(Messages.Validation.PasswordRequired)
            .MinimumLength(8).WithErrorCode(ErrorCodes.PasswordMinLength8).WithMessage(Messages.Validation.PasswordMinLength8);
    }
}
