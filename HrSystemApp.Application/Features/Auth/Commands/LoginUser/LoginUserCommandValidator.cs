using FluentValidation;

namespace HrSystemApp.Application.Features.Auth.Commands.LoginUser;

public class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");

        RuleFor(x => x.FcmToken)
            .NotEmpty().When(x => x.FcmToken != null).WithMessage("FCM token cannot be empty if provided.");

        RuleFor(x => x.DeviceType)
            .IsInEnum().When(x => x.DeviceType != null).WithMessage("Invalid device type specified.");

        RuleFor(x => x.Language)
            .NotEmpty().When(x => x.Language != null).WithMessage("Language cannot be empty if provided.")
            .MaximumLength(10).When(x => x.Language != null).WithMessage("Language code must not exceed 10 characters.");
    }
}
