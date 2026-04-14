using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.HierarchyLevels.Commands.UpdateHierarchyLevel;

public class UpdateHierarchyLevelCommandValidator : AbstractValidator<UpdateHierarchyLevelCommand>
{
    public UpdateHierarchyLevelCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(Messages.Validation.FieldRequired)
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");

        RuleFor(x => x.SortOrder)
            .GreaterThan(0).WithMessage("SortOrder must be a positive number.");
    }
}