using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Domain.Models;

public class AttendanceReminderLog : BaseEntity
{
    public Guid AttendanceId { get; set; }
    public Guid EmployeeId { get; set; }
    public AttendanceReminderType ReminderType { get; set; } = AttendanceReminderType.MissingClockOut;
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    public AttendanceReminderStatus Status { get; set; } = AttendanceReminderStatus.Sent;
    public string Channel { get; set; } = "Notification";
    public string? JobRunId { get; set; }
    public string WindowKey { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    public Attendance Attendance { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
