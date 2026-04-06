using FluentValidation;

namespace HrSystemApp.Application.Features.Companies.Commands.CreateCompany;

public class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(200).WithMessage("Company name cannot exceed 200 characters.");

        RuleFor(x => x.YearlyVacationDays)
            .NotNull().WithMessage("Yearly vacation days is required.");

        RuleFor(x => x.GraceMinutes)
            .GreaterThanOrEqualTo(0).WithMessage("Grace minutes must be zero or positive.");

        RuleFor(x => x.TimeZoneId)
            .NotEmpty().WithMessage("Time zone id is required.")
            .MaximumLength(100).WithMessage("Time zone id cannot exceed 100 characters.");
    }
}
