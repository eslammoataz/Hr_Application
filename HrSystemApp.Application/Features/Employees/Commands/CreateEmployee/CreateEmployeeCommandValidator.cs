using FluentValidation;
using HrSystemApp.Application.Resources;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Employees.Commands.CreateEmployee;

public class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithErrorCode(ErrorCodes.FullNameRequired).WithMessage(Messages.Validation.FullNameRequired)
            .MaximumLength(200).WithErrorCode(ErrorCodes.FullNameMaxLength).WithMessage(Messages.Validation.FullNameMaxLength);

        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode(ErrorCodes.EmailRequired).WithMessage(Messages.Validation.EmailRequired)
            .EmailAddress().WithErrorCode(ErrorCodes.ValidEmailRequired).WithMessage(Messages.Validation.ValidEmailRequired);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithErrorCode(ErrorCodes.PhoneRequired).WithMessage(Messages.Validation.PhoneRequired)
            .Matches(@"^\+?[0-9]{7,15}$").WithErrorCode(ErrorCodes.PhoneMustBeDigits).WithMessage(Messages.Validation.PhoneMustBeDigits);

        RuleFor(x => x.CompanyId)
            .NotEmpty().WithErrorCode(ErrorCodes.CompanyIdRequired).WithMessage(Messages.Validation.CompanyIdRequired);

        RuleFor(x => x.Role)
            .IsInEnum().WithErrorCode(ErrorCodes.InvalidRole).WithMessage(Messages.Validation.InvalidRole)
            .NotEqual(UserRole.SuperAdmin).WithErrorCode(ErrorCodes.CannotAssignSuperAdmin).WithMessage(Messages.Validation.CannotAssignSuperAdmin);
    }
}
