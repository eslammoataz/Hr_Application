using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.Requests.Queries.PreviewApprovalChain;

public record PreviewApprovalChainQuery : IRequest<Result<List<PlannedStepDto>>>
{
    /// <summary>Optional: preview a saved definition.</summary>
    public Guid? DefinitionId { get; set; }

    /// <summary>Optional: preview an inline draft (for admin UI authoring).</summary>
    public List<WorkflowStepDto>? Steps { get; set; }

    /// <summary>Required: the employee whose chain is being previewed.</summary>
    public Guid RequesterEmployeeId { get; set; }
}

public class PreviewApprovalChainQueryHandler : IRequestHandler<PreviewApprovalChainQuery, Result<List<PlannedStepDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkflowResolutionService _workflowResolutionService;
    private readonly ILogger<PreviewApprovalChainQueryHandler> _logger;

    public PreviewApprovalChainQueryHandler(
        IUnitOfWork unitOfWork,
        IWorkflowResolutionService workflowResolutionService,
        ILogger<PreviewApprovalChainQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _workflowResolutionService = workflowResolutionService;
        _logger = logger;
    }

    public async Task<Result<List<PlannedStepDto>>> Handle(PreviewApprovalChainQuery request, CancellationToken ct)
    {
        // Must supply either DefinitionId or inline Steps (not both, not neither).
        if (request.DefinitionId.HasValue == (request.Steps != null && request.Steps.Count > 0))
        {
            // Both or neither supplied.
            _logger.LogWarning("PreviewApprovalChain: must supply exactly one of DefinitionId or Steps");
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.General.ArgumentError);
        }

        List<WorkflowStepDto> stepsToResolve;
        if (request.DefinitionId.HasValue)
        {
            var definition = await _unitOfWork.RequestDefinitions.GetFirstOrDefaultAsync(
                d => d.Id == request.DefinitionId.Value, ct, d => d.WorkflowSteps);

            if (definition == null)
                return Result.Failure<List<PlannedStepDto>>(DomainErrors.Requests.DefinitionNotFound);

            stepsToResolve = definition.WorkflowSteps.Select(s => new WorkflowStepDto
            {
                StepType = s.StepType,
                OrgNodeId = s.OrgNodeId,
                BypassHierarchyCheck = s.BypassHierarchyCheck,
                DirectEmployeeId = s.DirectEmployeeId,
                StartFromLevel = s.StartFromLevel,
                LevelsUp = s.LevelsUp,
                SortOrder = s.SortOrder
            }).ToList();
        }
        else
        {
            stepsToResolve = request.Steps!;
        }

        // Look up the requester's node assignment (same as CreateRequestCommand).
        var assignment = await _unitOfWork.OrgNodeAssignments.GetByEmployeeWithNodeAsync(request.RequesterEmployeeId, ct);
        if (assignment == null)
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.OrgNode.NotFound);

        return await _workflowResolutionService.BuildApprovalChainAsync(
            request.RequesterEmployeeId,
            assignment.OrgNodeId,
            stepsToResolve,
            ct);
    }
}
