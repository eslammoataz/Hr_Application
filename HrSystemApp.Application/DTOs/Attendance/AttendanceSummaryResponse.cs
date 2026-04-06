namespace HrSystemApp.Application.DTOs.Attendance;

public sealed record AttendanceSummaryResponse(
    Guid EmployeeId,
    string EmployeeName,
    DateOnly Date,
    DateTime? FirstClockInUtc,
    DateTime? LastClockOutUtc,
    decimal TotalHours,
    string Status,
    bool IsLate,
    bool IsEarlyLeave,
    string? Reason);
