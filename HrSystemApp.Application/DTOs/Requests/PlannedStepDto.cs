namespace HrSystemApp.Application.DTOs.Requests;

public class PlannedStepDto
{
    public Guid NodeId { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<ApproverDto> Approvers { get; set; } = new();
}

public class ApproverDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
}

public class WorkflowStepDto
{
    public Guid OrgNodeId { get; set; }
    public int SortOrder { get; set; }
}