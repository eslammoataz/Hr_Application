using FluentValidation;

namespace HrSystemApp.Application.Features.Auth.Commands.ForceChangePassword;

public class ForceChangePasswordCommandValidator : AbstractValidator<ForceChangePasswordCommand>
{
    public ForceChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(6).WithMessage("New password must be at least 6 characters.")
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must be different from the current password.");
    }
}
