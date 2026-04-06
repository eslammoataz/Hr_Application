namespace HrSystemApp.Application.DTOs.Attendance;

public sealed record ShiftRulesUtc(
    DateOnly BusinessDate,
    TimeSpan StartTimeLocal,
    TimeSpan EndTimeLocal,
    int GraceMinutes,
    string TimeZoneId,
    DateTime ShiftStartUtc,
    DateTime ShiftEndUtc,
    DateTime LateThresholdUtc,
    DateTime ReminderDueUtc);
