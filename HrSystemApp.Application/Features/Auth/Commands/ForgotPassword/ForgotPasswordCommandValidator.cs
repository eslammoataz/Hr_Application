using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Auth.Commands.ForgotPassword;

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(Messages.Validation.EmailRequired)
            .EmailAddress().WithMessage(Messages.Validation.ValidEmailRequired);

        RuleFor(x => x.Channel)
            .IsInEnum().WithMessage(Messages.Validation.InvalidDeliveryChannel);
    }
}
