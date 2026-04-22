using FluentValidation;
using HrSystemApp.Application.Resources;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Auth.Commands.RegisterUser;

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode(ErrorCodes.EmailRequired).WithMessage(Messages.Validation.EmailRequired)
            .EmailAddress().WithErrorCode(ErrorCodes.ValidEmailRequired).WithMessage(Messages.Validation.ValidEmailRequired);

        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(ErrorCodes.NameRequired).WithMessage(Messages.Validation.NameRequired)
            .MaximumLength(100).WithErrorCode(ErrorCodes.NameMaxLength).WithMessage(Messages.Validation.NameMaxLength);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithErrorCode(ErrorCodes.PhoneRequired).WithMessage(Messages.Validation.PhoneRequired);

        RuleFor(x => x.Password)
            .NotEmpty().WithErrorCode(ErrorCodes.PasswordRequired).WithMessage(Messages.Validation.PasswordRequired)
            .MinimumLength(6).WithErrorCode(ErrorCodes.PasswordMinLength).WithMessage(Messages.Validation.PasswordMinLength);

        RuleFor(x => x.Role)
            .IsInEnum().WithErrorCode(ErrorCodes.InvalidRole).WithMessage(Messages.Validation.InvalidRole)
            .NotEqual(UserRole.SuperAdmin).WithErrorCode(ErrorCodes.CannotAssignSuperAdminRegistration).WithMessage(Messages.Validation.CannotAssignSuperAdminRegistration);
    }
}
