using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Companies.Commands.CreateCompany;

public class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty().WithErrorCode(ErrorCodes.CompanyNameRequiredForCompany).WithMessage(Messages.Validation.CompanyNameRequiredForCompany)
            .MaximumLength(200).WithErrorCode(ErrorCodes.CompanyNameMaxLengthForCompany).WithMessage(Messages.Validation.CompanyNameMaxLengthForCompany);

        RuleFor(x => x.YearlyVacationDays)
            .NotNull().WithErrorCode(ErrorCodes.YearlyVacationDaysRequired).WithMessage(Messages.Validation.YearlyVacationDaysRequired);

        RuleFor(x => x.GraceMinutes)
            .GreaterThanOrEqualTo(0).WithErrorCode(ErrorCodes.GraceMinutesMustBeNonNegative).WithMessage(Messages.Validation.GraceMinutesMustBeNonNegative);

        RuleFor(x => x.TimeZoneId)
            .NotEmpty().WithErrorCode(ErrorCodes.TimeZoneIdRequired).WithMessage(Messages.Validation.TimeZoneIdRequired)
            .MaximumLength(100).WithErrorCode(ErrorCodes.TimeZoneIdMaxLength).WithMessage(Messages.Validation.TimeZoneIdMaxLength);
    }
}
