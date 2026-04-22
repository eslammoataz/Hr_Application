using System.Diagnostics;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Infrastructure.Services;

public class WorkflowResolutionService : IWorkflowResolutionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WorkflowResolutionService> _logger;
    private readonly LoggingOptions _loggingOptions;

    public WorkflowResolutionService(
        IUnitOfWork unitOfWork,
        ILogger<WorkflowResolutionService> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<List<PlannedStepDto>>> BuildApprovalChainAsync(
        Guid requesterEmployeeId,
        Guid requesterNodeId,
        List<WorkflowStepDto> definitionSteps,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var action = LogAction.Workflow.CreateRequest;

        try
        {
            _logger.LogDecision(_loggingOptions, action, LogStage.ExternalCall,
                "GetAncestorsAsync", new { NodeId = requesterNodeId });

            var isManagerAtOwnNode = await _unitOfWork.OrgNodeAssignments
                .IsManagerAtNodeAsync(requesterEmployeeId, requesterNodeId, ct);

            var ownNode = await _unitOfWork.OrgNodes.GetByIdAsync(requesterNodeId, ct);
            if (ownNode == null)
            {
                _logger.LogDecision(_loggingOptions, action, LogStage.Processing,
                    "NodeNotFound", new { NodeId = requesterNodeId });
                return Result.Failure<List<PlannedStepDto>>(DomainErrors.OrgNode.NotFound);
            }

            var ancestors = await _unitOfWork.OrgNodes.GetAncestorsAsync(requesterNodeId, ct);
            var ancestorIds = ancestors.Select(a => a.Id).ToHashSet();

            _logger.LogExternalCall(_loggingOptions, action, "GetAncestorsAsync", sw.ElapsedMilliseconds);

            var levelNodes = new List<OrgNode> { ownNode };
            levelNodes.AddRange(ancestors);

            _logger.LogDecision(_loggingOptions, action, LogStage.Processing,
                "AncestorsResolved", new { AncestorCount = ancestors.Count });

            var sortedSteps = definitionSteps.OrderBy(s => s.SortOrder).ToList();

            var plannedSteps = new List<PlannedStepDto>();
            var seenApproverIds = new HashSet<Guid>();

            foreach (var step in sortedSteps)
            {
                if (step.StepType == WorkflowStepType.DirectEmployee)
                {
                    if (step.DirectEmployeeId == null)
                        return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingDirectEmployeeId);

                    var directEmployee = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, ct);
                    if (directEmployee == null)
                        return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.DirectEmployeeNotInCompany);

                    if (seenApproverIds.Contains(directEmployee.Id))
                        continue;

                    seenApproverIds.Add(directEmployee.Id);

                    plannedSteps.Add(new PlannedStepDto
                    {
                        StepType = WorkflowStepType.DirectEmployee,
                        NodeId = null,
                        NodeName = directEmployee.FullName,
                        SortOrder = 0,
                        Approvers = new List<ApproverDto>
                        {
                            new ApproverDto { EmployeeId = directEmployee.Id, EmployeeName = directEmployee.FullName }
                        }
                    });
                }
                else if (step.StepType == WorkflowStepType.HierarchyLevel)
                {
                    if (!step.LevelsUp.HasValue || step.LevelsUp.Value < 1)
                        return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingLevelsUp);

                    var startLevel = step.StartFromLevel ?? 1;
                    var endLevel = startLevel + step.LevelsUp.Value - 1;

                    for (int level = startLevel; level <= endLevel; level++)
                    {
                        if (level > levelNodes.Count)
                        {
                            _logger.LogDecision(_loggingOptions, action, LogStage.Processing,
                                "ChainExhausted", new { RequestedLevel = level, AvailableLevels = levelNodes.Count });
                            break;
                        }

                        var node = levelNodes[level - 1];
                        var managers = await _unitOfWork.OrgNodeAssignments.GetManagersByNodeAsync(node.Id, ct);

                        if (managers.Count == 0)
                            continue;

                        var approvers = managers
                            .Where(m => m.Id != requesterEmployeeId)
                            .Select(m => new ApproverDto { EmployeeId = m.Id, EmployeeName = m.FullName })
                            .ToList();

                        if (approvers.Count == 0)
                            continue;

                        approvers = approvers.Where(a => !seenApproverIds.Contains(a.EmployeeId)).ToList();
                        if (approvers.Count == 0)
                            continue;

                        foreach (var a in approvers) seenApproverIds.Add(a.EmployeeId);

                        plannedSteps.Add(new PlannedStepDto
                        {
                            StepType = WorkflowStepType.HierarchyLevel,
                            NodeId = node.Id,
                            NodeName = node.Name,
                            SortOrder = 0,
                            Approvers = approvers,
                            ResolvedFromLevel = level
                        });
                    }
                }
                else if (step.StepType == WorkflowStepType.CompanyRole)
                {
                    if (!step.CompanyRoleId.HasValue)
                        return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingCompanyRoleId);

                    var role = await _unitOfWork.CompanyRoles.GetByIdAsync(step.CompanyRoleId.Value, ct);
                    if (role is null)
                        return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.RoleNotFound);

                    var roleHolders = await _unitOfWork.EmployeeCompanyRoles
                        .GetActiveEmployeesByRoleIdAsync(step.CompanyRoleId.Value, ct);

                    var approvers = roleHolders
                        .Where(e => e.Id != requesterEmployeeId && !seenApproverIds.Contains(e.Id))
                        .Select(e => new ApproverDto { EmployeeId = e.Id, EmployeeName = e.FullName })
                        .ToList();

                    if (approvers.Count == 0)
                        continue;

                    foreach (var a in approvers) seenApproverIds.Add(a.EmployeeId);

                    plannedSteps.Add(new PlannedStepDto
                    {
                        StepType = WorkflowStepType.CompanyRole,
                        NodeId = null,
                        NodeName = role.Name,
                        CompanyRoleId = role.Id,
                        RoleName = role.Name,
                        SortOrder = 0,
                        Approvers = approvers
                    });
                }
                else
                {
                    if (step.OrgNodeId == null)
                        return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingOrgNodeId);

                    if (step.OrgNodeId == requesterNodeId && isManagerAtOwnNode)
                        continue;

                    if (!step.BypassHierarchyCheck)
                    {
                        if (step.OrgNodeId != requesterNodeId && !ancestorIds.Contains(step.OrgNodeId.Value))
                        {
                            _logger.LogDecision(_loggingOptions, action, LogStage.Processing,
                                "OrgNodeNotInPath", new { StepSortOrder = step.SortOrder, OrgNodeId = step.OrgNodeId });
                            return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.InvalidWorkflowChain);
                        }
                    }

                    OrgNode? stepNode = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId.Value, ct);
                    var nodeName = stepNode?.Name ?? step.OrgNodeId.ToString()!;

                    var managers = await _unitOfWork.OrgNodeAssignments
                        .GetManagersByNodeAsync(step.OrgNodeId!.Value, ct);

                    var approvers = managers
                        .Where(e => e.Id != requesterEmployeeId && !seenApproverIds.Contains(e.Id))
                        .Select(e => new ApproverDto { EmployeeId = e.Id, EmployeeName = e.FullName })
                        .ToList();

                    if (approvers.Count == 0)
                        continue;

                    foreach (var a in approvers) seenApproverIds.Add(a.EmployeeId);

                    plannedSteps.Add(new PlannedStepDto
                    {
                        StepType = WorkflowStepType.OrgNode,
                        NodeId = step.OrgNodeId,
                        NodeName = nodeName,
                        SortOrder = step.SortOrder,
                        Approvers = approvers
                    });
                }
            }

            for (int i = 0; i < plannedSteps.Count; i++)
                plannedSteps[i].SortOrder = i + 1;

            sw.Stop();
            _logger.LogActionSuccess(_loggingOptions, action, sw.ElapsedMilliseconds);

            if (plannedSteps.Count == 0)
            {
                _logger.LogDecision(_loggingOptions, action, LogStage.Processing,
                    "EmptyChain", new { EmployeeId = requesterEmployeeId, NodeId = requesterNodeId });
            }

            return Result.Success(plannedSteps);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var lastKnownState = new { EmployeeId = requesterEmployeeId, NodeId = requesterNodeId };
            _logger.LogActionFailure(_loggingOptions, action, LogStage.Processing, ex, lastKnownState);
            throw;
        }
    }

    public async Task<Result> ValidateWorkflowStepsAsync(
        Guid requesterEmployeeId,
        Guid requesterNodeId,
        List<WorkflowStepDto> definitionSteps,
        CancellationToken ct)
    {
        var chainResult = await BuildApprovalChainAsync(
            requesterEmployeeId,
            requesterNodeId,
            definitionSteps,
            ct);

        return chainResult.IsSuccess
            ? Result.Success()
            : Result.Failure(chainResult.Error);
    }
}
