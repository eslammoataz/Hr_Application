using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Domain.Models;

public class ProfileUpdateRequest : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public string ChangesJson { get; set; } = string.Empty;
    public ProfileUpdateRequestStatus Status { get; set; } = ProfileUpdateRequestStatus.Pending;
    public string? EmployeeComment { get; set; }
    public string? HrNote { get; set; }
    public DateTime? HandledAt { get; set; }
    public Guid? HandledByHrId { get; set; }

    // Navigation
    public Employee Employee { get; set; } = null!;
    public Employee? HandledByHr { get; set; }
}
