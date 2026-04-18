using HrSystemApp.Domain.Common;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Domain.Models;

/// <summary>
/// Defines the specific settings and existence of a request type for a company.
/// </summary>
public class RequestDefinition : AuditableEntity, IHardDelete
{
    public Guid CompanyId { get; set; }
    public RequestType RequestType { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Custom JSON schema for this specific request type (if any).
    /// Used for dynamic form validation on the backend and rendering on the frontend.
    /// </summary>
    public string? FormSchemaJson { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
    public ICollection<RequestWorkflowStep> WorkflowSteps { get; set; } = new List<RequestWorkflowStep>();
}

/// <summary>
/// A single step in an approval chain.
/// Can be either an OrgNode step (managers at a node approve) or a DirectEmployee step (specific employee approves).
/// </summary>
public class RequestWorkflowStep : BaseEntity
{
    public Guid RequestDefinitionId { get; set; }

    /// <summary>
    /// The type of this step - OrgNode or DirectEmployee.
    /// </summary>
    public WorkflowStepType StepType { get; set; } = WorkflowStepType.OrgNode;

    /// <summary>
    /// The OrgNode that this step targets.
    /// Approvers are the employees with OrgRole = Manager assigned to this node.
    /// NULL when StepType is DirectEmployee.
    /// </summary>
    public Guid? OrgNodeId { get; set; }

    /// <summary>
    /// For OrgNode steps: if true, bypasses ancestor validation.
    /// Allows referencing an HR node that is not in the requester's hierarchy.
    /// Ignored for DirectEmployee steps.
    /// </summary>
    public bool BypassHierarchyCheck { get; set; } = false;

    /// <summary>
    /// The specific employee who must approve this step.
    /// Only set when StepType is DirectEmployee.
    /// </summary>
    public Guid? DirectEmployeeId { get; set; }

    /// <summary>
    /// Sequence order (1, 2, 3...)
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// For HierarchyLevel steps: the first ancestor level to include (1-based).
    /// Level 1 = requester's own node. Level 2 = immediate parent. Etc.
    /// NULL for non-HierarchyLevel steps. Defaults to 1 if omitted on a HierarchyLevel step.
    /// </summary>
    public int? StartFromLevel { get; set; }

    /// <summary>
    /// For HierarchyLevel steps: how many consecutive levels this step covers starting at StartFromLevel.
    /// NULL for non-HierarchyLevel steps. Required (>= 1) for HierarchyLevel steps.
    /// </summary>
    public int? LevelsUp { get; set; }

    // Navigation
    public RequestDefinition RequestDefinition { get; set; } = null!;
    public OrgNode? OrgNode { get; set; }
    public Employee? DirectEmployee { get; set; }
}
