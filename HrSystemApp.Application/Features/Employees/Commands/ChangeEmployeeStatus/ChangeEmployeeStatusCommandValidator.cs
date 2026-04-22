using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Employees.Commands.ChangeEmployeeStatus;

public class ChangeEmployeeStatusCommandValidator : AbstractValidator<ChangeEmployeeStatusCommand>
{
    public ChangeEmployeeStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithErrorCode(ErrorCodes.FieldRequired).WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.Status)
            .IsInEnum().WithErrorCode(ErrorCodes.ChangeEmployeeStatusInvalid).WithMessage(Messages.Validation.InvalidEmploymentStatus);
    }
}
