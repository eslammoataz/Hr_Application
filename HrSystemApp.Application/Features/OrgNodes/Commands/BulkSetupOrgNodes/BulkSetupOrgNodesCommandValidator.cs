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
            .NotNull().WithErrorCode(ErrorCodes.BulkSetupRequestCannotBeNull).WithMessage(Messages.Validation.BulkSetupRequestCannotBeNull);

        RuleFor(x => x.Request.Nodes)
            .NotEmpty().WithErrorCode(ErrorCodes.BulkSetupAtLeastOneNode).WithMessage(Messages.Validation.BulkSetupAtLeastOneNode)
            .Must(nodes => nodes.Select(n => n.TempId).Distinct().Count() == nodes.Count)
            .WithErrorCode(ErrorCodes.BulkSetupTempIdsUnique).WithMessage(Messages.Validation.BulkSetupTempIdsUnique);

        RuleFor(x => x.Request.Nodes)
            .Must(nodes => nodes.All(n => string.IsNullOrEmpty(n.ParentTempId) || nodes.Any(n2 => n2.TempId == n.ParentTempId)))
            .WithErrorCode(ErrorCodes.BulkSetupParentTempIdInvalid).WithMessage(Messages.Validation.BulkSetupParentTempIdInvalid);

        RuleForEach(x => x.Request.Nodes).ChildRules(node =>
        {
            node.RuleFor(n => n.TempId)
                .NotEmpty().WithErrorCode(ErrorCodes.FieldRequired).WithMessage(Messages.Validation.FieldRequired);

            node.RuleFor(n => n.Name)
                .NotEmpty().WithErrorCode(ErrorCodes.FieldRequired).WithMessage(Messages.Validation.FieldRequired);

            node.RuleForEach(n => n.Assignments).ChildRules(assignment =>
            {
                assignment.RuleFor(a => a.EmployeeId)
                    .NotEmpty().WithErrorCode(ErrorCodes.FieldRequired).WithMessage(Messages.Validation.FieldRequired);
            });
        });
    }
}
