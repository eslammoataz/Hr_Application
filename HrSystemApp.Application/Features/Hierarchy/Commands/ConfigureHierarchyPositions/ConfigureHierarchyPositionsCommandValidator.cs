using FluentValidation;

namespace HrSystemApp.Application.Features.Hierarchy.Commands.ConfigureHierarchyPositions;

public class ConfigureHierarchyPositionsCommandValidator : AbstractValidator<ConfigureHierarchyPositionsCommand>
{
    public ConfigureHierarchyPositionsCommandValidator()
    {
        RuleFor(x => x.Positions)
            .NotEmpty().WithMessage("At least one hierarchy position is required.")
            .Must(positions => positions.Select(p => p.SortOrder).Distinct().Count() == positions.Count)
            .WithMessage("Duplicate sort orders are not allowed.")
            .Must(positions => positions.Select(p => p.Role).Distinct().Count() == positions.Count)
            .WithMessage("Duplicate roles are not allowed.");

        RuleForEach(x => x.Positions).ChildRules(position =>
        {
            position.RuleFor(p => p.PositionTitle)
                .NotEmpty().WithMessage("Position title is required.")
                .MaximumLength(200);

            position.RuleFor(p => p.SortOrder)
                .GreaterThan(0).WithMessage("Sort order must be greater than 0.");
        });
    }
}
