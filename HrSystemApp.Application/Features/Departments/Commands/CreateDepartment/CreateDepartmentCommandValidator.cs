using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Departments.Commands.CreateDepartment;

public class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator()
    {
        RuleFor(x => x.CompanyId).NotEmpty().WithMessage(Messages.Validation.CompanyIdRequiredForDepartment);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage(Messages.Validation.DepartmentNameRequired);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
    }
}
