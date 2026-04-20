using FluentValidation;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Resources;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.AssignEmployeeToNode;

public class AssignEmployeeToNodeCommandValidator : AbstractValidator<AssignEmployeeToNodeCommand>
{
    public AssignEmployeeToNodeCommandValidator(IUnitOfWork unitOfWork)
    {
        RuleFor(x => x.OrgNodeId)
            .NotEmpty().WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Invalid role specified.");

        RuleFor(x => x.EmployeeId)
            .MustAsync(async (employeeId, ct) =>
            {
                var assignments = await unitOfWork.OrgNodeAssignments.GetByEmployeeAsync(employeeId, ct);
                return assignments.Count == 0;
            })
            .WithMessage("Employee is already assigned to a node. Each employee can belong to only one node at a time.");
    }
}
