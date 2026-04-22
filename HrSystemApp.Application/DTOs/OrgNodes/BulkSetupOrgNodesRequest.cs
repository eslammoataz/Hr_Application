using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.OrgNodes;

public class BulkSetupOrgNodesRequest
{
    /// <summary>
    /// The company ID this hierarchy belongs to.
    /// All nodes in the hierarchy will be associated with this company.
    /// </summary>
    public Guid CompanyId { get; set; }

    public List<BulkOrgNodeDto> Nodes { get; set; } = new();
}

public class BulkOrgNodeDto
{
    public string TempId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? ParentTempId { get; set; }
    public List<BulkAssignmentDto> Assignments { get; set; } = new();
}

public class BulkAssignmentDto
{
    public Guid EmployeeId { get; set; }
    public OrgRole Role { get; set; }
}
