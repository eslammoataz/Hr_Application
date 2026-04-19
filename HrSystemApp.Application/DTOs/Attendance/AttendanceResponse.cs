namespace HrSystemApp.Application.DTOs.Attendance;

public sealed record AttendanceResponse(
    Guid AttendanceId,
    Guid EmployeeId,
    DateOnly Date,
    DateTime? FirstClockInUtc,
    DateTime? LastClockOutUtc,
    decimal TotalHours,
    string Status,
    bool IsLate,
    bool IsEarlyLeave,
    string? Reason,
    IReadOnlyList<AttendanceSessionDto> Sessions);
