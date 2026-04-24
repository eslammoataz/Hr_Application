using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Infrastructure.Services.Workflow;

public sealed class WorkflowResolutionContext
{
    public Guid RequesterEmployeeId { get; init; }
    public Guid RequesterNodeId { get; init; }
    public bool IsManagerAtOwnNode { get; init; }
    public IReadOnlyList<OrgNode> LevelNodes { get; init; } = [];
    public IReadOnlySet<Guid> AncestorIds { get; init; } = new HashSet<Guid>();
    public IReadOnlyDictionary<Guid, IReadOnlyList<Employee>> ManagersByNodeId { get; init; } = new Dictionary<Guid, IReadOnlyList<Employee>>();
    public IReadOnlyDictionary<Guid, Employee> EmployeesById { get; init; } = new Dictionary<Guid, Employee>();
    public IReadOnlyDictionary<Guid, CompanyRole> RolesById { get; init; } = new Dictionary<Guid, CompanyRole>();
    public IReadOnlyDictionary<Guid, IReadOnlyList<Employee>> RoleHoldersByRoleId { get; init; } = new Dictionary<Guid, IReadOnlyList<Employee>>();
    public IReadOnlyDictionary<Guid, OrgNode> OrgNodesById { get; init; } = new Dictionary<Guid, OrgNode>();
}

public sealed class WorkflowResolutionState
{
    private readonly HashSet<Guid> _seenApproverIds = new();

    public List<PlannedStepDto> PlannedSteps { get; } = [];
    public IReadOnlySet<Guid> SeenApproverIds => _seenApproverIds;

    public bool TryAddStep(PlannedStepDto step)
    {
        if (step.Approvers.Count == 0)
            return false;

        // Validate all approvers before mutating state, so a partial collision
        // never leaves SeenApproverIds in an inconsistent state.
        if (step.Approvers.Any(a => _seenApproverIds.Contains(a.EmployeeId)))
            return false;

        foreach (var approver in step.Approvers)
            _seenApproverIds.Add(approver.EmployeeId);

        step.SortOrder = PlannedSteps.Count + 1;
        PlannedSteps.Add(step);
        return true;
    }
}
