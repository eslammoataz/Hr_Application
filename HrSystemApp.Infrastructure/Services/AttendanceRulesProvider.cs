using HrSystemApp.Application.DTOs.Attendance;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Infrastructure.Services;

public class AttendanceRulesProvider : IAttendanceRulesProvider
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AttendanceRulesProvider> _logger;

    public AttendanceRulesProvider(ApplicationDbContext context, ILogger<AttendanceRulesProvider> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ShiftRulesUtc> GetRulesAsync(Guid employeeId, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var data = await _context.Employees
            .AsNoTracking()
            .Where(x => x.Id == employeeId)
            .Select(x => new
            {
                x.Id,
                x.Company.StartTime,
                x.Company.EndTime,
                x.Company.GraceMinutes,
                x.Company.TimeZoneId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (data is null)
        {
            throw new KeyNotFoundException(DomainErrors.Employee.NotFound.Message);
        }

        var timeZone = ResolveTimeZone(data.TimeZoneId);

        var localStart = businessDate.ToDateTime(TimeOnly.FromTimeSpan(data.StartTime), DateTimeKind.Unspecified);
        var localEnd = businessDate.ToDateTime(TimeOnly.FromTimeSpan(data.EndTime), DateTimeKind.Unspecified);
        if (data.EndTime <= data.StartTime)
        {
            localEnd = localEnd.AddDays(1);
        }

        var shiftStartUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone);
        var shiftEndUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone);
        var lateThresholdUtc = shiftStartUtc.AddMinutes(data.GraceMinutes);
        var reminderDueUtc = shiftEndUtc.AddMinutes(data.GraceMinutes);

        return new ShiftRulesUtc(
            businessDate,
            data.StartTime,
            data.EndTime,
            data.GraceMinutes,
            timeZone.Id,
            shiftStartUtc,
            shiftEndUtc,
            lateThresholdUtc,
            reminderDueUtc);
    }

    public async Task<DateOnly> ResolveBusinessDateAsync(Guid employeeId, DateTime timestampUtc, CancellationToken cancellationToken = default)
    {
        var data = await _context.Employees
            .AsNoTracking()
            .Where(x => x.Id == employeeId)
            .Select(x => new
            {
                x.Company.TimeZoneId,
                x.Company.StartTime,
                x.Company.EndTime
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (data is null || string.IsNullOrWhiteSpace(data.TimeZoneId))
        {
            return DateOnly.FromDateTime(timestampUtc);
        }

        var timeZone = ResolveTimeZone(data.TimeZoneId);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc), timeZone);
        var calendarDate = DateOnly.FromDateTime(localTime);

        // For overnight shifts: check if this timestamp still belongs to
        // the PREVIOUS calendar day's shift window.
        // Example: shift is 10 pm – 1 am. Employee clocks in at 12:30 am on Day 2.
        // localTime = 12:30 am → calendarDate = Day 2.
        // But Day 1's shift window covers 10 pm Day 1 → 1 am Day 2, so 12:30 am is still Day 1.
        if (data.EndTime <= data.StartTime)
        {
            var prevDate = calendarDate.AddDays(-1);

            var prevLocalStart = prevDate.ToDateTime(
                TimeOnly.FromTimeSpan(data.StartTime), DateTimeKind.Unspecified);
            var prevLocalEnd = prevDate.ToDateTime(
                TimeOnly.FromTimeSpan(data.EndTime), DateTimeKind.Unspecified)
                .AddDays(1); // end is on the following calendar day

            var prevShiftStartUtc = TimeZoneInfo.ConvertTimeToUtc(prevLocalStart, timeZone);
            var prevShiftEndUtc   = TimeZoneInfo.ConvertTimeToUtc(prevLocalEnd,   timeZone);

            if (timestampUtc >= prevShiftStartUtc && timestampUtc <= prevShiftEndUtc)
            {
                return prevDate;
            }
        }

        return calendarDate;
    }

    private TimeZoneInfo ResolveTimeZone(string? companyTimeZoneId)
    {
        if (string.IsNullOrWhiteSpace(companyTimeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(companyTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            _logger.LogWarning("Company time zone id {TimeZoneId} was not found. Falling back to UTC.", companyTimeZoneId);
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            _logger.LogWarning("Company time invalid time zone — falling back to UTC.");
            return TimeZoneInfo.Utc;
        }
    }
}
