using FluentValidation;

namespace HrSystemApp.Application.Features.ContactAdmin.Commands.CreateContactAdminRequest;

public class CreateContactAdminRequestCommandValidator : AbstractValidator<CreateContactAdminRequestCommand>
{
    public CreateContactAdminRequestCommandValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(v => v.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not valid.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.");

        RuleFor(v => v.CompanyName)
            .NotEmpty().WithMessage("Company Name is required.")
            .MaximumLength(200).WithMessage("Company Name must not exceed 200 characters.");

        RuleFor(v => v.PhoneNumber)
            .NotEmpty().WithMessage("Phone Number is required.")
            .MaximumLength(50).WithMessage("Phone Number must not exceed 50 characters.");
    }
}
