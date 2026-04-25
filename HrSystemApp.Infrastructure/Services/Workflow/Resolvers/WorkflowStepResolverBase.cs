using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Infrastructure.Services.Workflow.Resolvers;

public abstract class WorkflowStepResolverBase : IWorkflowStepResolver
{
    public abstract WorkflowStepType Type { get; }

    public abstract Task<Result<List<PlannedStepDto>>> ResolveAsync(
        WorkflowStepDto step,
        WorkflowResolutionContext context,
        WorkflowResolutionState state,
        CancellationToken ct);

    protected static List<ApproverDto> FilterApprovers(
        IEnumerable<Employee> employees,
        Guid requesterEmployeeId,
        IReadOnlySet<Guid> seenApproverIds)
    {
        return employees
            .Where(e => e.Id != requesterEmployeeId && !seenApproverIds.Contains(e.Id))
            .Select(e => new ApproverDto { EmployeeId = e.Id, EmployeeName = e.FullName })
            .ToList();
    }

    protected static PlannedStepDto CreateStep(
        WorkflowStepType stepType,
        string nodeName,
        List<ApproverDto> approvers,
        Guid? nodeId = null,
        Guid? companyRoleId = null,
        string? roleName = null,
        int? resolvedFromLevel = null)
    {
        return new PlannedStepDto
        {
            StepType = stepType,
            NodeId = nodeId,
            NodeName = nodeName,
            CompanyRoleId = companyRoleId,
            RoleName = roleName,
            ResolvedFromLevel = resolvedFromLevel,
            SortOrder = 0,
            Approvers = approvers
        };
    }
}
