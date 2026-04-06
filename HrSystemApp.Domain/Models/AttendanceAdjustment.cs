namespace HrSystemApp.Domain.Models;

public class AttendanceAdjustment : BaseEntity
{
    public Guid AttendanceId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string BeforeSnapshotJson { get; set; } = string.Empty;
    public string AfterSnapshotJson { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Attendance Attendance { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
