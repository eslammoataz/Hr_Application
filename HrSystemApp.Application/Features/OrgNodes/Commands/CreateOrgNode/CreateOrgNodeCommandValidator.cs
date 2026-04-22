using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.CreateOrgNode;

public class CreateOrgNodeCommandValidator : AbstractValidator<CreateOrgNodeCommand>
{
    public CreateOrgNodeCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(ErrorCodes.FieldRequired).WithMessage(Messages.Validation.FieldRequired)
            .MaximumLength(200).WithErrorCode(ErrorCodes.OrgNodeNameMaxLength).WithMessage(Messages.Validation.OrgNodeNameMaxLength);
    }
}
