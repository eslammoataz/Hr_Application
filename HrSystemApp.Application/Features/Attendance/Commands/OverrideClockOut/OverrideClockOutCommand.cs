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

namespace HrSystemApp.Application.Features.Attendance.Commands.OverrideClockOut;

public sealed record OverrideClockOutCommand(
    Guid EmployeeId,
    DateOnly Date,
    DateTime ClockOutUtc,
    string Reason) : IRequest<Result<AttendanceResponse>>;

public class OverrideClockOutCommandHandler : IRequestHandler<OverrideClockOutCommand, Result<AttendanceResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttendanceRulesProvider _attendanceRulesProvider;
    private readonly ICurrentUserService _currentUserService;

    public OverrideClockOutCommandHandler(
        IUnitOfWork unitOfWork,
        IAttendanceRulesProvider attendanceRulesProvider,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _attendanceRulesProvider = attendanceRulesProvider;
        _currentUserService = currentUserService;
    }

    public async Task<Result<AttendanceResponse>> Handle(OverrideClockOutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result.Failure<AttendanceResponse>(DomainErrors.Attendance.OverrideReasonRequired);
        }

        var callerUserId = _currentUserService.UserId;
        var callerEmployee = await _unitOfWork.Employees.GetByUserIdAsync(callerUserId, cancellationToken);

        var targetEmployee = await _unitOfWork.Employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (targetEmployee == null)
            return Result.Failure<AttendanceResponse>(DomainErrors.Employee.NotFound);

        if (callerEmployee?.CompanyId != targetEmployee.CompanyId)
            return Result.Failure<AttendanceResponse>(DomainErrors.Auth.Unauthorized);

        var attendance = await _unitOfWork.Attendances.GetByEmployeeAndDateAsync(request.EmployeeId, request.Date, cancellationToken);
        if (attendance is null)
        {
            return Result.Failure<AttendanceResponse>(DomainErrors.Attendance.NotFound);
        }

        // Use the most recent ClockIn to validate the override timestamp and accumulate hours correctly.
        var lastClockIn = await _unitOfWork.AttendanceLogs.GetLastClockInAsync(attendance.Id, cancellationToken);
        var sessionStartUtc = lastClockIn?.TimestampUtc ?? attendance.FirstClockInUtc;

        if (sessionStartUtc is null || request.ClockOutUtc <= sessionStartUtc.Value)
        {
            return Result.Failure<AttendanceResponse>(DomainErrors.Attendance.InvalidClockOut);
        }

        var rules = await _attendanceRulesProvider.GetRulesAsync(request.EmployeeId, request.Date, cancellationToken);
        var normalizedClockOut = DateTime.SpecifyKind(request.ClockOutUtc, DateTimeKind.Utc);
        var beforeSnapshot = BuildSnapshot(attendance);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var log = new AttendanceLog
            {
                AttendanceId = attendance.Id,
                EmployeeId = attendance.EmployeeId,
                TimestampUtc = normalizedClockOut,
                Type = AttendanceLogType.ClockOut,
                Reason = request.Reason,
                Source = AttendanceLogSource.Admin,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _unitOfWork.AttendanceLogs.AddAsync(log, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            attendance.LastClockOutLogId = log.Id;
            attendance.LastClockOutUtc = normalizedClockOut;
            attendance.IsEarlyLeave = normalizedClockOut < rules.ShiftEndUtc;
            attendance.Reason = request.Reason;

            // Recalculate TotalHours from ALL logs so that overriding a session
            // does not double-count hours that were already accumulated.
            var allLogs = await _unitOfWork.AttendanceLogs
                .GetByAttendanceIdAsync(attendance.Id, cancellationToken);
            attendance.TotalHours = AttendanceSummaryCalculator.CalculateTotalHoursFromLogs(allLogs);

            attendance.Status = AttendanceSummaryCalculator.ResolveStatus(
                attendance.IsLate, attendance.IsEarlyLeave, hasClockOut: true);

            await _unitOfWork.Attendances.UpdateAsync(attendance, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var afterSnapshot = BuildSnapshot(attendance);

            var adjustment = new AttendanceAdjustment
            {
                AttendanceId = attendance.Id,
                EmployeeId = attendance.EmployeeId,
                Reason = request.Reason,
                UpdatedByUserId = _currentUserService.UserId ?? "system",
                BeforeSnapshotJson = beforeSnapshot,
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
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            attendance.FirstClockInUtc,
            attendance.LastClockOutUtc,
            attendance.TotalHours,
            Status = attendance.Status.ToString(),
            attendance.IsLate,
            attendance.IsEarlyLeave,
            attendance.Reason
        });
    }
}
