using FluentValidation;
using HrSystemApp.Application.Resources;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Employees.Commands.CreateEmployee;

public class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage(Messages.Validation.FullNameRequired)
            .MaximumLength(200).WithMessage(Messages.Validation.FullNameMaxLength);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(Messages.Validation.EmailRequired)
            .EmailAddress().WithMessage(Messages.Validation.ValidEmailRequired);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage(Messages.Validation.PhoneRequired)
            .Matches(@"^\+?[0-9]{7,15}$").WithMessage(Messages.Validation.PhoneMustBeDigits);

        RuleFor(x => x.CompanyId)
            .NotEmpty().WithMessage(Messages.Validation.CompanyIdRequired);

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage(Messages.Validation.InvalidRole)
            .NotEqual(UserRole.SuperAdmin).WithMessage(Messages.Validation.CannotAssignSuperAdmin);
    }
}
