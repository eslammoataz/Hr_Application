using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Domain.Models;

public class Attendance : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public DateOnly Date { get; set; }
    public DateTime? FirstClockInUtc { get; set; }
    public DateTime? LastClockOutUtc { get; set; }
    public decimal TotalHours { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Absent;
    public bool IsLate { get; set; }
    public bool IsEarlyLeave { get; set; }
    public string? Reason { get; set; }
    public Guid? FirstClockInLogId { get; set; }
    public Guid? LastClockOutLogId { get; set; }
    public uint Xmin { get; set; }

    public Employee Employee { get; set; } = null!;
    public AttendanceLog? FirstClockInLog { get; set; }
    public AttendanceLog? LastClockOutLog { get; set; }
    public ICollection<AttendanceLog> Logs { get; set; } = new List<AttendanceLog>();
    public ICollection<AttendanceReminderLog> ReminderLogs { get; set; } = new List<AttendanceReminderLog>();
    public ICollection<AttendanceAdjustment> Adjustments { get; set; } = new List<AttendanceAdjustment>();
}
