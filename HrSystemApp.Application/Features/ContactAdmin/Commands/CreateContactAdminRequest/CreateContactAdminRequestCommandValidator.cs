using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.ContactAdmin.Commands.CreateContactAdminRequest;

public class CreateContactAdminRequestCommandValidator : AbstractValidator<CreateContactAdminRequestCommand>
{
    public CreateContactAdminRequestCommandValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty().WithMessage(Messages.Validation.NameRequired)
            .MaximumLength(200).WithMessage(Messages.Validation.NameMaxLength);

        RuleFor(v => v.Email)
            .NotEmpty().WithMessage(Messages.Validation.EmailRequired)
            .EmailAddress().WithMessage(Messages.Validation.EmailNotValid)
            .MaximumLength(255).WithMessage(Messages.Validation.EmailMaxLength);

        RuleFor(v => v.CompanyName)
            .NotEmpty().WithMessage(Messages.Validation.CompanyNameRequired)
            .MaximumLength(200).WithMessage(Messages.Validation.CompanyNameMaxLength);

        RuleFor(v => v.PhoneNumber)
            .NotEmpty().WithMessage(Messages.Validation.PhoneNumberRequired)
            .MaximumLength(50).WithMessage(Messages.Validation.PhoneNumberMaxLength);
    }
}
