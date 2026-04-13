using FluentValidation;
using HrSystemApp.Application.Resources;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Auth.Commands.RegisterUser;

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(Messages.Validation.EmailRequired)
            .EmailAddress().WithMessage(Messages.Validation.ValidEmailRequired);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(Messages.Validation.NameRequired)
            .MaximumLength(100).WithMessage(Messages.Validation.NameMaxLength);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage(Messages.Validation.PhoneRequired);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(Messages.Validation.PasswordRequired)
            .MinimumLength(6).WithMessage(Messages.Validation.PasswordMinLength);

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage(Messages.Validation.InvalidRole)
            .NotEqual(UserRole.SuperAdmin).WithMessage(Messages.Validation.CannotAssignSuperAdminRegistration);
    }
}
