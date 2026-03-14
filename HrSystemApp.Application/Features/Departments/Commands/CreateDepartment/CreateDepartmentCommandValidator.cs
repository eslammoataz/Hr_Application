using FluentValidation;

namespace HrSystemApp.Application.Features.Departments.Commands.CreateDepartment;

public class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator()
    {
        RuleFor(x => x.CompanyId).NotEmpty().WithMessage("CompanyId is required.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Department name is required and must not exceed 200 characters.");
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
    }
}
