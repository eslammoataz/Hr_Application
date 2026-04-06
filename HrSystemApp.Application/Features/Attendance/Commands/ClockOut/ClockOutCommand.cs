using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Attendance;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Attendance.Common;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.Attendance.Commands.ClockOut;

public sealed record ClockOutCommand(DateTime? TimestampUtc = null) : IRequest<Result<AttendanceResponse>>;

public class ClockOutCommandHandler : IRequestHandler<ClockOutCommand, Result<AttendanceResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAttendanceRulesProvider _attendanceRulesProvider;

    public ClockOutCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IAttendanceRulesProvider attendanceRulesProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _attendanceRulesProvider = attendanceRulesProvider;
    }

    public async Task<Result<AttendanceResponse>> Handle(ClockOutCommand request, CancellationToken cancellationToken)
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

        var clockOutUtc = DateTime.SpecifyKind(request.TimestampUtc ?? DateTime.UtcNow, DateTimeKind.Utc);
        var attendance = await _unitOfWork.Attendances.GetOpenAttendanceAsync(employee.Id, cancellationToken);
        if (attendance is null)
        {
            return Result.Failure<AttendanceResponse>(DomainErrors.Attendance.ClockInRequired);
        }

        if (attendance.LastClockOutUtc is not null)
        {
            return Result.Failure<AttendanceResponse>(DomainErrors.Attendance.AlreadyClockedOut);
        }

        if (attendance.FirstClockInUtc is null || clockOutUtc <= attendance.FirstClockInUtc.Value)
        {
            return Result.Failure<AttendanceResponse>(DomainErrors.Attendance.InvalidClockOut);
        }

        var rules = await _attendanceRulesProvider.GetRulesAsync(employee.Id, attendance.Date, cancellationToken);
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var log = new AttendanceLog
            {
                AttendanceId = attendance.Id,
                EmployeeId = employee.Id,
                TimestampUtc = clockOutUtc,
                Type = AttendanceLogType.ClockOut,
                Source = AttendanceLogSource.Employee,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _unitOfWork.AttendanceLogs.AddAsync(log, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            attendance.LastClockOutLogId = log.Id;
            AttendanceSummaryCalculator.ApplyClockOut(attendance, clockOutUtc, rules.ShiftEndUtc);

            await _unitOfWork.Attendances.UpdateAsync(attendance, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

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
                attendance.Reason));
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
