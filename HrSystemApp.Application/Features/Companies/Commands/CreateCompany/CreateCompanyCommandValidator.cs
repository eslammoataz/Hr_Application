using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Companies.Commands.CreateCompany;

public class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage(Messages.Validation.CompanyNameRequiredForCompany)
            .MaximumLength(200).WithMessage(Messages.Validation.CompanyNameMaxLengthForCompany);

        RuleFor(x => x.YearlyVacationDays)
            .NotNull().WithMessage(Messages.Validation.YearlyVacationDaysRequired);

        RuleFor(x => x.GraceMinutes)
            .GreaterThanOrEqualTo(0).WithMessage(Messages.Validation.GraceMinutesMustBeNonNegative);

        RuleFor(x => x.TimeZoneId)
            .NotEmpty().WithMessage(Messages.Validation.TimeZoneIdRequired)
            .MaximumLength(100).WithMessage(Messages.Validation.TimeZoneIdMaxLength);
    }
}
