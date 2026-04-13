using FluentValidation;

namespace HrSystemApp.Application.Features.Companies.Commands.UpdateCompany;

public class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Company ID is required.");

        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(300).WithMessage("Company name must not exceed 300 characters.");

        RuleFor(x => x.GraceMinutes)
            .GreaterThanOrEqualTo(0).WithMessage("Grace minutes cannot be negative.")
            .LessThanOrEqualTo(120).WithMessage("Grace minutes cannot exceed 120.");

        RuleFor(x => x.YearlyVacationDays)
            .GreaterThan(0).WithMessage("Yearly vacation days must be greater than 0.")
            .LessThanOrEqualTo(365).WithMessage("Yearly vacation days cannot exceed 365.");

        RuleFor(x => x.TimeZoneId)
            .NotEmpty().WithMessage("Time zone is required.")
            .MaximumLength(100);

        RuleFor(x => x)
            .Must(x => x.StartTime < x.EndTime)
            .WithMessage("Start time must be before end time.");
    }
}
