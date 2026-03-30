using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Domain.Models;

/// <summary>
/// Defines the specific settings and existence of a request type for a company.
/// </summary>
public class RequestDefinition : AuditableEntity
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
/// A single step in an approval chain, defining a required role.
/// e.g. Step 1: TeamLeader, Step 2: HR.
/// </summary>
public class RequestWorkflowStep : BaseEntity
{
    public Guid RequestDefinitionId { get; set; }
    
    /// <summary>
    /// The role required at this step. 
    /// The system will find the specific employee occupying this role for the requester.
    /// </summary>
    public UserRole RequiredRole { get; set; }
    
    /// <summary>
    /// Sequence order (1, 2, 3...)
    /// </summary>
    public int SortOrder { get; set; }

    // Navigation
    public RequestDefinition RequestDefinition { get; set; } = null!;
}
