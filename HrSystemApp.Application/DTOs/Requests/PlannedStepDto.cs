using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.Requests;

public class ApproverDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
}

public class PlannedStepDto
{
    public WorkflowStepType StepType { get; set; } = WorkflowStepType.OrgNode;
    public Guid? NodeId { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int? ResolvedFromLevel { get; set; }
    public Guid? CompanyRoleId { get; set; }
    public string? RoleName { get; set; }
    public List<ApproverDto> Approvers { get; set; } = new();
}

public class WorkflowStepDto
{
    public WorkflowStepType StepType { get; set; } = WorkflowStepType.OrgNode;

    /// <summary>
    /// Human-readable name of the step type: "OrgNode", "DirectEmployee", "HierarchyLevel", or "CompanyRole".
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
    /// For HierarchyLevel steps: how many ancestor levels to include starting from StartFromLevel.
    /// Must be >= 1. Null on other step types.
    /// </summary>
    public int? LevelsUp { get; set; }

    /// <summary>
    /// For CompanyRole steps: the ID of the company role whose members will approve this step.
    /// Null on other step types.
    /// </summary>
    public Guid? CompanyRoleId { get; set; }

    /// <summary>
    /// Determines the order in which steps are executed. Must be unique across all steps in a definition.
    /// </summary>
    public int SortOrder { get; set; }
}
