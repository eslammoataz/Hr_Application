using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Employees.Commands.UpdateEmployee;

public class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithErrorCode(ErrorCodes.FieldRequired).WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.FullName)
            .MaximumLength(200).When(x => x.FullName is not null)
            .WithErrorCode(ErrorCodes.EmployeeFullNameMaxLength).WithMessage(Messages.Validation.FullNameMaxLength);

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).When(x => x.PhoneNumber is not null)
            .WithErrorCode(ErrorCodes.EmployeePhoneMaxLength).WithMessage(Messages.Validation.EmployeePhoneMaxLength);

        RuleFor(x => x.Address)
            .MaximumLength(500).When(x => x.Address is not null)
            .WithErrorCode(ErrorCodes.EmployeeAddressMaxLength).WithMessage(Messages.Validation.EmployeeAddressMaxLength);
    }
}
