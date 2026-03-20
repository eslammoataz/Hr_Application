using FluentValidation;

namespace HrSystemApp.Application.Features.Companies.Commands.CreateCompanyLocation;

public class CreateCompanyLocationCommandValidator : AbstractValidator<CreateCompanyLocationCommand>
{
    public CreateCompanyLocationCommandValidator()
    {
        RuleFor(x => x.CompanyId)
            .NotEmpty().WithMessage("Company ID is required.");

        RuleFor(x => x.LocationName)
            .NotEmpty().WithMessage("Location name is required.")
            .MaximumLength(100).WithMessage("Location name cannot exceed 100 characters.");
    }
}
