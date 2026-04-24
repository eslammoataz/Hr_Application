using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Attendance;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Attendance.Common;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.Attendance.Queries.GetAttendanceSessions;

public sealed record GetAttendanceSessionsQuery(Guid AttendanceId)
    : IRequest<Result<IReadOnlyList<AttendanceSessionDto>>>;

public class GetAttendanceSessionsQueryHandler
    : IRequestHandler<GetAttendanceSessionsQuery, Result<IReadOnlyList<AttendanceSessionDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetAttendanceSessionsQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Handles a request to retrieve session summaries for a specified attendance record.
    /// </summary>
    /// <returns>
    /// A <see cref="Result{T}"/> containing a read-only list of <see cref="AttendanceSessionDto"/> on success;
    /// a failure result with <c>DomainErrors.Attendance.NotFound</c> if the attendance does not exist,
    /// or <c>DomainErrors.General.Forbidden</c> if the current user is not authorized to view the attendance.
    /// </returns>
    public async Task<Result<IReadOnlyList<AttendanceSessionDto>>> Handle(
        GetAttendanceSessionsQuery request, CancellationToken cancellationToken)
    {
        var attendance = await _unitOfWork.Attendances
            .GetByIdAsync(request.AttendanceId, cancellationToken);
        if (attendance is null)
        {
            return Result.Failure<IReadOnlyList<AttendanceSessionDto>>(DomainErrors.Attendance.NotFound);
        }

        var currentUserId = _currentUserService.UserId;
        var currentUserRole = _currentUserService.Role;
        var isHrOrAbove = currentUserRole is not null &&
            Enum.TryParse<UserRole>(currentUserRole, out var role) &&
            role is UserRole.SuperAdmin or UserRole.Executive or UserRole.HR or UserRole.CompanyAdmin;

        var currentEmployee = await _unitOfWork.Employees.GetByUserIdAsync(currentUserId, cancellationToken);
        var isOwner = currentEmployee?.Id == attendance.EmployeeId;

        if (isHrOrAbove && currentEmployee is not null && currentEmployee.CompanyId != attendance.Employee.CompanyId)
            isHrOrAbove = false;

        if (!isHrOrAbove && !isOwner)
        {
            return Result.Failure<IReadOnlyList<AttendanceSessionDto>>(DomainErrors.General.Forbidden);
        }

        var logs = await _unitOfWork.AttendanceLogs
            .GetByAttendanceIdAsync(request.AttendanceId, cancellationToken);

        return Result.Success(AttendanceSummaryCalculator.BuildSessions(logs));
    }
}
