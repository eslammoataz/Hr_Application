using FluentValidation;

namespace HrSystemApp.Application.Features.Employees.Commands.ChangeEmployeeStatus;

public class ChangeEmployeeStatusCommandValidator : AbstractValidator<ChangeEmployeeStatusCommand>
{
    public ChangeEmployeeStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Employee ID is required.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid employment status value.");
    }
}
