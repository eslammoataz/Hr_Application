using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Companies.Commands.CreateCompanyLocation;

public class CreateCompanyLocationCommandValidator : AbstractValidator<CreateCompanyLocationCommand>
{
    public CreateCompanyLocationCommandValidator()
    {
        RuleFor(x => x.CompanyId)
            .NotEmpty().WithMessage(Messages.Validation.CompanyIdRequired);

        RuleFor(x => x.LocationName)
            .NotEmpty().WithMessage(Messages.Validation.LocationNameRequired)
            .MaximumLength(100).WithMessage(Messages.Validation.LocationNameMaxLength);
    }
}
