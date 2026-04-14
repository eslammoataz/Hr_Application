using FluentValidation;
using HrSystemApp.Application.Resources;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.AssignEmployeeToNode;

public class AssignEmployeeToNodeCommandValidator : AbstractValidator<AssignEmployeeToNodeCommand>
{
    public AssignEmployeeToNodeCommandValidator()
    {
        RuleFor(x => x.OrgNodeId)
            .NotEmpty().WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Invalid role specified.");
    }
}