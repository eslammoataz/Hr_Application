using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Companies.Commands.UpdateMyCompany;

public class UpdateMyCompanyCommandValidator : AbstractValidator<UpdateMyCompanyCommand>
{
    public UpdateMyCompanyCommandValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty().WithErrorCode(ErrorCodes.UpdateMyCompanyNameRequired).WithMessage(Messages.Validation.FieldRequired)
            .MaximumLength(300).WithErrorCode(ErrorCodes.UpdateMyCompanyNameMaxLength).WithMessage(Messages.Validation.CompanyNameMaxLengthForCompany);

        RuleFor(x => x.GraceMinutes)
            .GreaterThanOrEqualTo(0).WithErrorCode(ErrorCodes.UpdateMyCompanyGraceMinutesNegative).WithMessage(Messages.Validation.UpdateCompanyGraceMinutesNegative)
            .LessThanOrEqualTo(120).WithErrorCode(ErrorCodes.UpdateMyCompanyGraceMinutesExceed).WithMessage(Messages.Validation.UpdateCompanyGraceMinutesExceed);

        RuleFor(x => x.YearlyVacationDays)
            .GreaterThan(0).WithErrorCode(ErrorCodes.UpdateMyCompanyVacationDaysPositive).WithMessage(Messages.Validation.UpdateCompanyVacationDaysPositive)
            .LessThanOrEqualTo(365).WithErrorCode(ErrorCodes.UpdateMyCompanyVacationDaysExceed).WithMessage(Messages.Validation.UpdateCompanyVacationDaysExceed);

        RuleFor(x => x.TimeZoneId)
            .NotEmpty().WithErrorCode(ErrorCodes.UpdateMyCompanyTimeZoneRequired).WithMessage(Messages.Validation.FieldRequired)
            .MaximumLength(100).WithErrorCode(ErrorCodes.UpdateMyCompanyTimeZoneMaxLength).WithMessage(Messages.Validation.UpdateCompanyTimeZoneMaxLength);

        RuleFor(x => x)
            .Must(x => x.StartTime < x.EndTime)
            .WithErrorCode(ErrorCodes.UpdateMyCompanyStartBeforeEnd).WithMessage(Messages.Validation.UpdateCompanyStartBeforeEnd);
    }
}
