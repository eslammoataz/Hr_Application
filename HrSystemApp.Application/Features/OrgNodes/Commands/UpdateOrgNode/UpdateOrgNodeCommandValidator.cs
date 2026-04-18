using FluentValidation;
using HrSystemApp.Application.Resources;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.UpdateOrgNode;

public class UpdateOrgNodeCommandValidator : AbstractValidator<UpdateOrgNodeCommand>
{
    public UpdateOrgNodeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(Messages.Validation.FieldRequired)
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters.");
    }
}