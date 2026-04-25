using HrSystemApp.Domain.Common;

namespace HrSystemApp.Domain.Models;

/// <summary>
/// Database-backed request type entity. Replaces the old RequestType enum.
/// Each record represents a request type (Leave, Permission, custom types, etc.).
/// </summary>
public class RequestType : AuditableEntity, IHardDelete
{
    /// <summary>
    /// String key identifier (e.g., "Leave", "Permission", "CustomType").
    /// </summary>
    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the request type.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// If true, this is a system type (cannot be modified/deleted by companies).
    /// </summary>
    public bool IsSystemType { get; set; }

    /// <summary>
    /// If true, this is a custom type created by a company.
    /// </summary>
    public bool IsCustomType { get; set; }

    /// <summary>
    /// For custom types: the company that owns this request type.
    /// Null for system types.
    /// </summary>
    public Guid? CompanyId { get; set; }

    /// <summary>
    /// JSON schema for validating the request's DynamicDataJson.
    /// </summary>
    public string? FormSchemaJson { get; set; }

    /// <summary>
    /// If true, extra fields not in FormSchemaJson are allowed.
    /// </summary>
    public bool AllowExtraFields { get; set; } = true;

    /// <summary>
    /// Pattern for generating request numbers (e.g., "{KeyName}-{Year}-{Sequence:0000}").
    /// </summary>
    public string? RequestNumberPattern { get; set; }

    /// <summary>
    /// Default SLA in days for requests of this type.
    /// </summary>
    public int? DefaultSlaDays { get; set; }

    /// <summary>
    /// Localized display names as JSON (e.g., {"en": "Leave", "ar": "إجازة"}).
    /// </summary>
    public string? DisplayNameLocalizationsJson { get; set; }

    /// <summary>
    /// Schema version for audit trail.
    /// </summary>
    public int Version { get; set; } = 1;

    // Navigation
    public Company? Company { get; set; }
    public ICollection<Request> Requests { get; set; } = new List<Request>();
    public ICollection<RequestDefinition> RequestDefinitions { get; set; } = new List<RequestDefinition>();
}
