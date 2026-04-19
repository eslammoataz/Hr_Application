namespace HrSystemApp.Application.DTOs.Attendance;

/// <summary>
/// Represents a single worked session within an attendance day
/// (one ClockIn paired with its corresponding ClockOut).
/// SessionEndUtc is null when the employee is still clocked in.
/// </summary>
public sealed record AttendanceSessionDto(
    DateTime SessionStartUtc,
    DateTime? SessionEndUtc,
    decimal? DurationHours);
