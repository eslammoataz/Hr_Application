using FluentValidation;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.OrgNodes;
using HrSystemApp.Application.Resources;
using MediatR;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.BulkSetupOrgNodes;

public record BulkSetupOrgNodesCommand(BulkSetupOrgNodesRequest Request)
    : IRequest<Result<BulkSetupOrgNodesResponse>>;

public class BulkSetupOrgNodesCommandValidator : AbstractValidator<BulkSetupOrgNodesCommand>
{
    public BulkSetupOrgNodesCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request cannot be null.");

        RuleFor(x => x.Request.Nodes)
            .NotEmpty().WithMessage("At least one node is required.")
            .Must(nodes => nodes.Select(n => n.TempId).Distinct().Count() == nodes.Count)
            .WithMessage("All tempIds must be unique.");

        RuleFor(x => x.Request.Nodes)
            .Must(nodes => nodes.All(n => string.IsNullOrEmpty(n.ParentTempId) || nodes.Any(n2 => n2.TempId == n.ParentTempId)))
            .WithMessage("ParentTempId must reference a valid TempId in the request.");

        RuleForEach(x => x.Request.Nodes).ChildRules(node =>
        {
            node.RuleFor(n => n.TempId)
                .NotEmpty().WithMessage(Messages.Validation.FieldRequired);

            node.RuleFor(n => n.Name)
                .NotEmpty().WithMessage(Messages.Validation.FieldRequired);

            node.RuleForEach(n => n.Assignments).ChildRules(assignment =>
            {
                assignment.RuleFor(a => a.EmployeeId)
                    .NotEmpty().WithMessage(Messages.Validation.FieldRequired);
            });
        });
    }
}