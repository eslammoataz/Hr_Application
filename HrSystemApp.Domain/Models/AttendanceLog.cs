using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Domain.Models;

public class AttendanceLog : BaseEntity
{
    public Guid AttendanceId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public AttendanceLogType Type { get; set; }
    public string? Reason { get; set; }
    public AttendanceLogSource Source { get; set; } = AttendanceLogSource.Employee;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? IdempotencyKey { get; set; }

    public Attendance Attendance { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
