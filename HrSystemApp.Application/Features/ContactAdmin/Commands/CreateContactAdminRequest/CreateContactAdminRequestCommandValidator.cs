using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.ContactAdmin.Commands.CreateContactAdminRequest;

public class CreateContactAdminRequestCommandValidator : AbstractValidator<CreateContactAdminRequestCommand>
{
    public CreateContactAdminRequestCommandValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty().WithErrorCode(ErrorCodes.NameRequired).WithMessage(Messages.Validation.NameRequired)
            .MaximumLength(200).WithErrorCode(ErrorCodes.NameMaxLength).WithMessage(Messages.Validation.NameMaxLength);

        RuleFor(v => v.Email)
            .NotEmpty().WithErrorCode(ErrorCodes.EmailRequired).WithMessage(Messages.Validation.EmailRequired)
            .EmailAddress().WithErrorCode(ErrorCodes.EmailNotValid).WithMessage(Messages.Validation.EmailNotValid)
            .MaximumLength(255).WithErrorCode(ErrorCodes.EmailMaxLength).WithMessage(Messages.Validation.EmailMaxLength);

        RuleFor(v => v.CompanyName)
            .NotEmpty().WithErrorCode(ErrorCodes.CompanyNameRequired).WithMessage(Messages.Validation.CompanyNameRequired)
            .MaximumLength(200).WithErrorCode(ErrorCodes.CompanyNameMaxLength).WithMessage(Messages.Validation.CompanyNameMaxLength);

        RuleFor(v => v.PhoneNumber)
            .NotEmpty().WithErrorCode(ErrorCodes.PhoneNumberRequired).WithMessage(Messages.Validation.PhoneNumberRequired)
            .MaximumLength(50).WithErrorCode(ErrorCodes.PhoneNumberMaxLength).WithMessage(Messages.Validation.PhoneNumberMaxLength);
    }
}
