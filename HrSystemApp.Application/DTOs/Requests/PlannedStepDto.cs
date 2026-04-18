using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.Requests;

public class WorkflowStepDto
{
    public WorkflowStepType StepType { get; set; } = WorkflowStepType.OrgNode;
    public Guid? OrgNodeId { get; set; }
    public bool BypassHierarchyCheck { get; set; } = false;
    public Guid? DirectEmployeeId { get; set; }
    public int SortOrder { get; set; }
}

public class PlannedStepDto
{
    public WorkflowStepType StepType { get; set; } = WorkflowStepType.OrgNode;
    public Guid? NodeId { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<ApproverDto> Approvers { get; set; } = new();
}

public class ApproverDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
}
