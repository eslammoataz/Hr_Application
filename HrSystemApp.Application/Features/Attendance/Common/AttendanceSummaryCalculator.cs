using HrSystemApp.Domain.Enums;

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

    public static void ApplyClockOut(global::HrSystemApp.Domain.Models.Attendance attendance, DateTime clockOutUtc,
        DateTime shiftEndUtc, string? reason = null)
    {
        attendance.LastClockOutUtc = clockOutUtc;
        attendance.IsEarlyLeave = clockOutUtc < shiftEndUtc;
        attendance.Reason = reason;

        if (attendance.FirstClockInUtc.HasValue)
        {
            var total = clockOutUtc - attendance.FirstClockInUtc.Value;
            attendance.TotalHours = Math.Round((decimal)Math.Max(total.TotalHours, 0), 2);
        }

        attendance.Status = ResolveStatus(attendance.IsLate, attendance.IsEarlyLeave,
            attendance.LastClockOutUtc is not null);
    }

    public static AttendanceStatus ResolveStatus(bool isLate, bool isEarlyLeave, bool hasClockOut)
    {
        if (!hasClockOut)
        {
            return isLate ? AttendanceStatus.Late : AttendanceStatus.Present;
        }

        if (isLate && isEarlyLeave)
        {
            return AttendanceStatus.Late;
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
}
