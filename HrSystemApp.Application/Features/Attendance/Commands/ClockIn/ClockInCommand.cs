using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Attendance;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Attendance.Common;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.Attendance.Commands.ClockIn;

public sealed record ClockInCommand(DateTime? TimestampUtc = null) : IRequest<Result<AttendanceResponse>>;

public class ClockInCommandHandler : IRequestHandler<ClockInCommand, Result<AttendanceResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAttendanceRulesProvider _attendanceRulesProvider;

    public ClockInCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IAttendanceRulesProvider attendanceRulesProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _attendanceRulesProvider = attendanceRulesProvider;
    }

    public async Task<Result<AttendanceResponse>> Handle(ClockInCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result.Failure<AttendanceResponse>(DomainErrors.Auth.Unauthorized);
        }

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee is null)
        {
            return Result.Failure<AttendanceResponse>(DomainErrors.Employee.NotFound);
        }

        var clockInUtc = DateTime.SpecifyKind(request.TimestampUtc ?? DateTime.UtcNow, DateTimeKind.Utc);
        var businessDate =
            await _attendanceRulesProvider.ResolveBusinessDateAsync(employee.Id, clockInUtc, cancellationToken);
        var rules = await _attendanceRulesProvider.GetRulesAsync(employee.Id, businessDate, cancellationToken);

        var existingOpen = await _unitOfWork.Attendances.GetOpenAttendanceAsync(employee.Id, cancellationToken);
        if (existingOpen is not null)
        {
            return Result.Failure<AttendanceResponse>(DomainErrors.Attendance.AlreadyClockedIn);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var attendance =
                await _unitOfWork.Attendances.GetByEmployeeAndDateAsync(employee.Id, businessDate, cancellationToken);
            if (attendance is null)
            {
                attendance = new Domain.Models.Attendance
                {
                    EmployeeId = employee.Id,
                    Date = businessDate
                };
                await _unitOfWork.Attendances.AddAsync(attendance, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var log = new AttendanceLog
            {
                AttendanceId = attendance.Id,
                EmployeeId = employee.Id,
                TimestampUtc = clockInUtc,
                Type = AttendanceLogType.ClockIn,
                Source = AttendanceLogSource.Employee,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _unitOfWork.AttendanceLogs.AddAsync(log, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Only point FirstClockInLogId at the very first clock-in of the day.
            // Subsequent clock-ins (after a clock-out break) must not overwrite it.
            if (attendance.FirstClockInUtc is null)
            {
                attendance.FirstClockInLogId = log.Id;
            }

            // Re-open the attendance record so GetOpenAttendanceAsync can find it again.
            // TotalHours is intentionally kept so accumulated time from previous sessions is preserved.
            attendance.LastClockOutUtc = null;
            attendance.LastClockOutLogId = null;

            AttendanceSummaryCalculator.ApplyClockIn(attendance, clockInUtc, rules.LateThresholdUtc);
            await _unitOfWork.Attendances.UpdateAsync(attendance, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            var logs = await _unitOfWork.AttendanceLogs
                .GetByAttendanceIdAsync(attendance.Id, cancellationToken);
            var sessions = AttendanceSummaryCalculator.BuildSessions(logs);

            return Result.Success(new AttendanceResponse(
                attendance.Id,
                attendance.EmployeeId,
                attendance.Date,
                attendance.FirstClockInUtc,
                attendance.LastClockOutUtc,
                attendance.TotalHours,
                attendance.Status.ToString(),
                attendance.IsLate,
                attendance.IsEarlyLeave,
                attendance.Reason,
                sessions));
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}