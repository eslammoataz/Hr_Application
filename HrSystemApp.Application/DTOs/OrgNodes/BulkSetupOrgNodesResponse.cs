namespace HrSystemApp.Application.DTOs.OrgNodes;

public class BulkSetupOrgNodesResponse
{
    public Guid CompanyNodeId { get; set; }
    public List<BulkNodeResultDto> Nodes { get; set; } = new();
}

public class BulkNodeResultDto
{
    public string TempId { get; set; } = string.Empty;
    public Guid RealId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Depth { get; set; }
}