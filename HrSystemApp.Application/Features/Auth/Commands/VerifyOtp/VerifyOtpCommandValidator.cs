using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Auth.Commands.VerifyOtp;

public class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(Messages.Validation.EmailRequired)
            .EmailAddress().WithMessage(Messages.Validation.ValidEmailRequired);

        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage(Messages.Validation.OtpRequired)
            .Length(6).WithMessage(Messages.Validation.OtpMustBe6Chars)
            .Matches("^[0-9]{6}$").WithMessage(Messages.Validation.OtpMustBeNumeric);
    }
}
