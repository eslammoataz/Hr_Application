using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.Requests;

public class WorkflowStepDto
{
    public WorkflowStepType StepType { get; set; } = WorkflowStepType.OrgNode;

    /// <summary>
    /// Human-readable name of the step type: "OrgNode", "DirectEmployee", or "HierarchyLevel".
    /// </summary>
    public string StepTypeName { get; set; } = string.Empty;

    public Guid? OrgNodeId { get; set; }
    public bool BypassHierarchyCheck { get; set; } = false;
    public Guid? DirectEmployeeId { get; set; }

    /// <summary>
    /// For HierarchyLevel steps: first ancestor level to include (1-based). Null on other step types.
    /// Defaults to 1 when omitted on a HierarchyLevel step.
    /// </summary>
    public int? StartFromLevel { get; set; }

    /// <summary>
    /// For HierarchyLevel steps: how many consecutive levels covered. Required (>=1) for HierarchyLevel. Null on other step types.
    /// </summary>
    public int? LevelsUp { get; set; }

    public int SortOrder { get; set; }
}

public class PlannedStepDto
{
    public WorkflowStepType StepType { get; set; } = WorkflowStepType.OrgNode;
    public Guid? NodeId { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<ApproverDto> Approvers { get; set; } = new();

    /// <summary>
    /// For planned steps produced by a HierarchyLevel definition step:
    /// which ancestor level (1-based) this resolved step came from. Null for OrgNode/DirectEmployee steps.
    /// </summary>
    public int? ResolvedFromLevel { get; set; }
}

public class ApproverDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
}
