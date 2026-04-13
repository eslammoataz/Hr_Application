namespace HrSystemApp.Application.DTOs.Hierarchy;

public class HierarchyNodeMetadata
{
    // Employee specific
    public string? FullName { get; set; }
    public string? Role { get; set; }
    public string? Email { get; set; }
    public string? EmployeeCode { get; set; }
    public string? ManagerName { get; set; }
    public Guid? ManagerId { get; set; }
    
    // Organization specific
    public string? Name { get; set; }
    public string? LeaderName { get; set; }
    
    // Common
    public bool HasChildren { get; set; }
}
