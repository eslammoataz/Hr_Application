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
            .NotEmpty().WithErrorCode(ErrorCodes.FieldRequired).WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithErrorCode(ErrorCodes.FieldRequired).WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.Role)
            .IsInEnum().WithErrorCode(ErrorCodes.InvalidRole).WithMessage(Messages.Validation.InvalidRole);

        RuleFor(x => x.EmployeeId)
            .MustAsync(async (employeeId, ct) =>
            {
                var assignments = await unitOfWork.OrgNodeAssignments.GetByEmployeeAsync(employeeId, ct);
                return assignments.Count == 0;
            })
            .WithErrorCode(ErrorCodes.AssignEmployeeAlreadyAssigned).WithMessage(Messages.Validation.AssignEmployeeAlreadyAssigned);
    }
}
