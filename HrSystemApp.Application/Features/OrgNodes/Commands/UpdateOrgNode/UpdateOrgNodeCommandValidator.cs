using FluentValidation;
using HrSystemApp.Application.Resources;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.UpdateOrgNode;

public class UpdateOrgNodeCommandValidator : AbstractValidator<UpdateOrgNodeCommand>
{
    public UpdateOrgNodeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithErrorCode(ErrorCodes.FieldRequired).WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(ErrorCodes.FieldRequired).WithMessage(Messages.Validation.FieldRequired)
            .MaximumLength(200).WithErrorCode(ErrorCodes.OrgNodeNameMaxLength).WithMessage(Messages.Validation.OrgNodeNameMaxLength);
    }
}
