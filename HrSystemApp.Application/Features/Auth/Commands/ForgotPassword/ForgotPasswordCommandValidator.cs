using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Auth.Commands.ForgotPassword;

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode(ErrorCodes.EmailRequired).WithMessage(Messages.Validation.EmailRequired)
            .EmailAddress().WithErrorCode(ErrorCodes.ValidEmailRequired).WithMessage(Messages.Validation.ValidEmailRequired);

        RuleFor(x => x.Channel)
            .IsInEnum().WithErrorCode(ErrorCodes.InvalidDeliveryChannel).WithMessage(Messages.Validation.InvalidDeliveryChannel);
    }
}
