using System.Diagnostics;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly LoggingOptions _loggingOptions;

    public PreviewApprovalChainQueryHandler(
        IUnitOfWork unitOfWork,
        IWorkflowResolutionService workflowResolutionService,
        ILogger<PreviewApprovalChainQueryHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _workflowResolutionService = workflowResolutionService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<List<PlannedStepDto>>> Handle(PreviewApprovalChainQuery request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.Workflow.PreviewApprovalChain);

        if (request.DefinitionId.HasValue == (request.Steps != null && request.Steps.Count > 0))
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.PreviewApprovalChain, LogStage.Validation,
                "InvalidInput", "must supply exactly one of DefinitionId or Steps");
            sw.Stop();
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.General.ArgumentError);
        }

        List<WorkflowStepDto> stepsToResolve;
        if (request.DefinitionId.HasValue)
        {
            var definition = await _unitOfWork.RequestDefinitions.GetFirstOrDefaultAsync(
                d => d.Id == request.DefinitionId.Value, ct, d => d.WorkflowSteps);

            if (definition == null)
            {
                _logger.LogDecision(_loggingOptions, LogAction.Workflow.PreviewApprovalChain, LogStage.Validation,
                    "DefinitionNotFound", new { DefinitionId = request.DefinitionId.Value });
                sw.Stop();
                return Result.Failure<List<PlannedStepDto>>(DomainErrors.Requests.DefinitionNotFound);
            }

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

        var assignment = await _unitOfWork.OrgNodeAssignments.GetByEmployeeWithNodeAsync(request.RequesterEmployeeId, ct);
        if (assignment == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.PreviewApprovalChain, LogStage.Validation,
                "RequesterNodeNotFound", new { RequesterEmployeeId = request.RequesterEmployeeId });
            sw.Stop();
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.OrgNode.NotFound);
        }

        var result = await _workflowResolutionService.BuildApprovalChainAsync(
            request.RequesterEmployeeId,
            assignment.OrgNodeId,
            stepsToResolve,
            ct);

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.Workflow.PreviewApprovalChain, sw.ElapsedMilliseconds);

        return result;
    }
}
