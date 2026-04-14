using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.CreateOrgNode;

public class CreateOrgNodeCommandValidator : AbstractValidator<CreateOrgNodeCommand>
{
    public CreateOrgNodeCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(Messages.Validation.FieldRequired)
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters.");
    }
}
