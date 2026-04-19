using HrSystemApp.Application.DTOs.Attendance;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Features.Attendance.Common;

public static class AttendanceSummaryCalculator
{
    public static void ApplyClockIn(Domain.Models.Attendance attendance, DateTime clockInUtc, DateTime lateThresholdUtc)
    {
        if (attendance.FirstClockInUtc is null || clockInUtc < attendance.FirstClockInUtc)
        {
            attendance.FirstClockInUtc = clockInUtc;
        }

        attendance.IsLate = attendance.FirstClockInUtc > lateThresholdUtc;
        attendance.Status = ResolveStatus(attendance.IsLate, attendance.IsEarlyLeave,
            attendance.LastClockOutUtc is not null);
    }

    /// <summary>
    /// Applies a clock-out to the attendance record.
    /// </summary>
    /// <param name="attendance">The attendance record to update.</param>
    /// <param name="clockOutUtc">The clock-out timestamp in UTC.</param>
    /// <param name="shiftEndUtc">The shift end time in UTC, used to determine early leave.</param>
    /// <param name="sessionStartUtc">
    ///   The start of the current open session (i.e. the most recent ClockIn timestamp).
    ///   Only the duration of this session is added to <see cref="Attendance.TotalHours"/>,
    ///   which preserves accumulated hours from any previous sessions in the same day.
    /// </param>
    /// <param name="reason">Optional reason for the clock-out (e.g. admin override).</param>
    public static void ApplyClockOut(global::HrSystemApp.Domain.Models.Attendance attendance, DateTime clockOutUtc,
        DateTime shiftEndUtc, DateTime sessionStartUtc, string? reason = null)
    {
        attendance.LastClockOutUtc = clockOutUtc;
        attendance.IsEarlyLeave = clockOutUtc < shiftEndUtc;
        attendance.Reason = reason;

        // Accumulate only the current session's duration so that break time between
        // multiple clock-in/clock-out pairs in the same day is not counted as worked time.
        var sessionDuration = Math.Max((clockOutUtc - sessionStartUtc).TotalHours, 0);
        attendance.TotalHours += Math.Round((decimal)sessionDuration, 2);

        attendance.Status = ResolveStatus(attendance.IsLate, attendance.IsEarlyLeave,
            attendance.LastClockOutUtc is not null);
    }

    public static AttendanceStatus ResolveStatus(bool isLate, bool isEarlyLeave, bool hasClockOut)
    {
        if (!hasClockOut)
        {
            return isLate ? AttendanceStatus.Late : AttendanceStatus.Present;
        }

        if (isLate)
        {
            return AttendanceStatus.Late;
        }

        if (isEarlyLeave)
        {
            return AttendanceStatus.EarlyLeave;
        }

        return AttendanceStatus.Present;
    }

    /// <summary>
    /// Calculates total worked hours from a set of attendance logs by pairing
    /// ClockIn and ClockOut entries chronologically.
    /// An unpaired ClockIn (open session) contributes zero hours.
    /// </summary>
    public static decimal CalculateTotalHoursFromLogs(IReadOnlyList<AttendanceLog> logs)
    {
        var clockIns = logs
            .Where(l => l.Type == AttendanceLogType.ClockIn)
            .OrderBy(l => l.TimestampUtc)
            .ToList();

        var clockOuts = logs
            .Where(l => l.Type == AttendanceLogType.ClockOut)
            .OrderBy(l => l.TimestampUtc)
            .ToList();

        decimal total = 0m;
        int pairs = Math.Min(clockIns.Count, clockOuts.Count);
        for (int i = 0; i < pairs; i++)
        {
            var duration = (clockOuts[i].TimestampUtc - clockIns[i].TimestampUtc).TotalHours;
            total += Math.Round((decimal)Math.Max(duration, 0), 2);
        }

        return Math.Round(total, 2);
    }

    /// <summary>
    /// Builds a list of clock-in/clock-out session pairs from attendance logs.
    /// The last session will have SessionEndUtc = null if the employee is still clocked in.
    /// </summary>
    public static IReadOnlyList<AttendanceSessionDto> BuildSessions(IReadOnlyList<AttendanceLog> logs)
    {
        var clockIns = logs
            .Where(l => l.Type == AttendanceLogType.ClockIn)
            .OrderBy(l => l.TimestampUtc)
            .ToList();

        var clockOuts = logs
            .Where(l => l.Type == AttendanceLogType.ClockOut)
            .OrderBy(l => l.TimestampUtc)
            .ToList();

        var sessions = new List<AttendanceSessionDto>(clockIns.Count);
        for (int i = 0; i < clockIns.Count; i++)
        {
            var start = clockIns[i].TimestampUtc;
            DateTime? end = i < clockOuts.Count ? clockOuts[i].TimestampUtc : null;
            decimal? duration = end.HasValue
                ? Math.Round((decimal)Math.Max((end.Value - start).TotalHours, 0), 2)
                : null;

            sessions.Add(new AttendanceSessionDto(start, end, duration));
        }

        return sessions;
    }
}
