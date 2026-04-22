using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Companies.Commands.UpdateCompany;

public class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithErrorCode(ErrorCodes.FieldRequired).WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.CompanyName)
            .NotEmpty().WithErrorCode(ErrorCodes.UpdateCompanyNameRequired).WithMessage(Messages.Validation.FieldRequired)
            .MaximumLength(300).WithErrorCode(ErrorCodes.UpdateCompanyNameMaxLength).WithMessage(Messages.Validation.CompanyNameMaxLengthForCompany);

        RuleFor(x => x.GraceMinutes)
            .GreaterThanOrEqualTo(0).WithErrorCode(ErrorCodes.UpdateCompanyGraceMinutesNegative).WithMessage(Messages.Validation.UpdateCompanyGraceMinutesNegative)
            .LessThanOrEqualTo(120).WithErrorCode(ErrorCodes.UpdateCompanyGraceMinutesExceed).WithMessage(Messages.Validation.UpdateCompanyGraceMinutesExceed);

        RuleFor(x => x.YearlyVacationDays)
            .GreaterThan(0).WithErrorCode(ErrorCodes.UpdateCompanyVacationDaysPositive).WithMessage(Messages.Validation.UpdateCompanyVacationDaysPositive)
            .LessThanOrEqualTo(365).WithErrorCode(ErrorCodes.UpdateCompanyVacationDaysExceed).WithMessage(Messages.Validation.UpdateCompanyVacationDaysExceed);

        RuleFor(x => x.TimeZoneId)
            .NotEmpty().WithErrorCode(ErrorCodes.UpdateCompanyTimeZoneRequired).WithMessage(Messages.Validation.FieldRequired)
            .MaximumLength(100).WithErrorCode(ErrorCodes.UpdateCompanyTimeZoneMaxLength).WithMessage(Messages.Validation.UpdateCompanyTimeZoneMaxLength);

        RuleFor(x => x)
            .Must(x => x.StartTime < x.EndTime)
            .WithErrorCode(ErrorCodes.UpdateCompanyStartBeforeEnd).WithMessage(Messages.Validation.UpdateCompanyStartBeforeEnd);
    }
}
