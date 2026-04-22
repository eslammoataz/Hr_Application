using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Companies.Commands.CreateCompanyLocation;

public class CreateCompanyLocationCommandValidator : AbstractValidator<CreateCompanyLocationCommand>
{
    public CreateCompanyLocationCommandValidator()
    {
        RuleFor(x => x.CompanyId)
            .NotEmpty().WithErrorCode(ErrorCodes.CompanyIdRequired).WithMessage(Messages.Validation.CompanyIdRequired);

        RuleFor(x => x.LocationName)
            .NotEmpty().WithErrorCode(ErrorCodes.LocationNameRequired).WithMessage(Messages.Validation.LocationNameRequired)
            .MaximumLength(100).WithErrorCode(ErrorCodes.LocationNameMaxLength).WithMessage(Messages.Validation.LocationNameMaxLength);
    }
}
