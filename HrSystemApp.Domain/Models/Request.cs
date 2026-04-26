using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Domain.Models;

/// <summary>
/// Entity for all requests.
/// Specific types (Leave, Asset, etc.) store their unique data in the DynamicDataJson (JSON) column.
/// </summary>
public class Request : AuditableEntity
{
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Foreign key to the RequestType entity (database-backed, replacing the old enum).
    /// </summary>
    public Guid RequestTypeId { get; set; }

    /// <summary>
    /// Type-specific data (e.g. { "startDate": "...", "duration": 5 })
    /// </summary>
    public string DynamicDataJson { get; set; } = "{}";

    public RequestStatus Status { get; set; } = RequestStatus.Submitted;

    public string? Details { get; set; }

    /// <summary>
    /// The current step order (1-based). When CurrentStepOrder > step count, request is Approved.
    /// When request is Rejected, CurrentStepOrder = 0.
    /// </summary>
    public int CurrentStepOrder { get; set; } = 1;

    /// <summary>
    /// Snapshotted approval path at the time of submission.
    /// Structure: [{nodeId, nodeName, sortOrder, approvers: [{employeeId, employeeName}]}]
    /// </summary>
    public string? PlannedStepsJson { get; set; }

    /// <summary>
    /// Denormalized list of current step approver IDs for fast database filtering.
    /// Format: "empId1,empId2,empId3"
    /// Updated when request is created and when step advances.
    /// </summary>
    public string? CurrentStepApproverIds { get; set; }

    /// <summary>
    /// Schema that was active when this request was created.
    /// Captured at creation time so schema changes to the RequestType don't affect existing requests.
    /// </summary>
    public string? CapturedSchemaJson { get; set; }

    /// <summary>
    /// Auto-generated request number (e.g., "LV-2026-0001").
    /// </summary>
    public string? RequestNumber { get; set; }

    /// <summary>
    /// Calculated due date based on SLA.
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Set when SLA is breached (background service updates this).
    /// </summary>
    public DateTime? SlaBreachedAt { get; set; }

    // Navigation
    public Employee Employee { get; set; } = null!;
    public RequestType RequestType { get; set; } = null!;
    public ICollection<RequestApprovalHistory> ApprovalHistory { get; set; } = new List<RequestApprovalHistory>();
    public ICollection<RequestAttachment> Attachments { get; set; } = new List<RequestAttachment>();
}

/// <summary>
/// Historical record of approvals/rejections.
/// </summary>
public class RequestApprovalHistory : BaseEntity
{
    public Guid RequestId { get; set; }
    public Guid ApproverId { get; set; }
    public RequestStatus Status { get; set; } // Approved or Rejected
    public string? Comment { get; set; }

    // Navigation
    public Request Request { get; set; } = null!;
    public Employee Approver { get; set; } = null!;
}

/// <summary>
/// Simple attachment record (DB only).
/// </summary>
public class RequestAttachment : BaseEntity
{
    public Guid RequestId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;

    // Navigation
    public Request Request { get; set; } = null!;
}
