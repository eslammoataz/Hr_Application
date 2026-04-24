using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Attendance;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Attendance.Common;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.Attendance.Commands.OverrideClockIn;

public sealed record OverrideClockInCommand(
    Guid EmployeeId,
    DateOnly Date,
    DateTime ClockInUtc,
    string Reason) : IRequest<Result<AttendanceResponse>>;

public class OverrideClockInCommandHandler : IRequestHandler<OverrideClockInCommand, Result<AttendanceResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttendanceRulesProvider _attendanceRulesProvider;
    private readonly ICurrentUserService _currentUserService;

    public OverrideClockInCommandHandler(
        IUnitOfWork unitOfWork,
        IAttendanceRulesProvider attendanceRulesProvider,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _attendanceRulesProvider = attendanceRulesProvider;
        _currentUserService = currentUserService;
    }

    public async Task<Result<AttendanceResponse>> Handle(OverrideClockInCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result.Failure<AttendanceResponse>(DomainErrors.Attendance.OverrideReasonRequired);
        }

        if (request.ClockInUtc > DateTime.UtcNow.AddMinutes(5))
        {
            return Result.Failure<AttendanceResponse>(DomainErrors.Attendance.InvalidClockIn);
        }

        var callerUserId = _currentUserService.UserId;
        var callerEmployee = await _unitOfWork.Employees.GetByUserIdAsync(callerUserId, cancellationToken);

        var targetEmployee = await _unitOfWork.Employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (targetEmployee == null)
            return Result.Failure<AttendanceResponse>(DomainErrors.Employee.NotFound);

        if (callerEmployee?.CompanyId != targetEmployee.CompanyId)
            return Result.Failure<AttendanceResponse>(DomainErrors.Auth.Unauthorized);

        var normalizedClockIn = DateTime.SpecifyKind(request.ClockInUtc, DateTimeKind.Utc);
        var rules = await _attendanceRulesProvider.GetRulesAsync(request.EmployeeId, request.Date, cancellationToken);

        var attendance = await _unitOfWork.Attendances
            .GetByEmployeeAndDateAsync(request.EmployeeId, request.Date, cancellationToken);

        var beforeSnapshot = attendance is not null
            ? BuildSnapshot(attendance)
            : null;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (attendance is null)
            {
                attendance = new Domain.Models.Attendance
                {
                    EmployeeId = request.EmployeeId,
                    Date = request.Date
                };
                await _unitOfWork.Attendances.AddAsync(attendance, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var log = new AttendanceLog
            {
                AttendanceId = attendance.Id,
                EmployeeId = request.EmployeeId,
                TimestampUtc = normalizedClockIn,
                Type = AttendanceLogType.ClockIn,
                Source = AttendanceLogSource.Admin,
                Reason = request.Reason,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _unitOfWork.AttendanceLogs.AddAsync(log, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            attendance.FirstClockInUtc   = normalizedClockIn;
            attendance.FirstClockInLogId = log.Id;
            attendance.IsLate = normalizedClockIn > rules.LateThresholdUtc;

            // If the attendance is already completed (has a clock-out), recalculate TotalHours
            // from all logs because changing the first clock-in changes the first session's duration.
            if (attendance.LastClockOutUtc is not null)
            {
                var allLogs = await _unitOfWork.AttendanceLogs
                    .GetByAttendanceIdAsync(attendance.Id, cancellationToken);
                attendance.TotalHours = AttendanceSummaryCalculator.CalculateTotalHoursFromLogs(allLogs);
            }

            attendance.Status = AttendanceSummaryCalculator.ResolveStatus(
                attendance.IsLate, attendance.IsEarlyLeave,
                attendance.LastClockOutUtc is not null);

            await _unitOfWork.Attendances.UpdateAsync(attendance, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var afterSnapshot = BuildSnapshot(attendance);

            var adjustment = new AttendanceAdjustment
            {
                AttendanceId = attendance.Id,
                EmployeeId = attendance.EmployeeId,
                Reason = request.Reason,
                UpdatedByUserId = _currentUserService.UserId ?? "system",
                BeforeSnapshotJson = beforeSnapshot ?? "{}",
                AfterSnapshotJson = afterSnapshot,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _unitOfWork.AttendanceAdjustments.AddAsync(adjustment, cancellationToken);
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

    private static string BuildSnapshot(global::HrSystemApp.Domain.Models.Attendance attendance)
    {
        return JsonSerializer.Serialize(new
        {
            attendance.FirstClockInUtc,
            attendance.LastClockOutUtc,
            attendance.TotalHours,
            Status = AttendanceStatus.Present
        });
    }
}
