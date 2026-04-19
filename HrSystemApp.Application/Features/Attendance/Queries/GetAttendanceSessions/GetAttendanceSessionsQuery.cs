using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Attendance;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Attendance.Common;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Attendance.Queries.GetAttendanceSessions;

public sealed record GetAttendanceSessionsQuery(Guid AttendanceId)
    : IRequest<Result<IReadOnlyList<AttendanceSessionDto>>>;

public class GetAttendanceSessionsQueryHandler
    : IRequestHandler<GetAttendanceSessionsQuery, Result<IReadOnlyList<AttendanceSessionDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAttendanceSessionsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<AttendanceSessionDto>>> Handle(
        GetAttendanceSessionsQuery request, CancellationToken cancellationToken)
    {
        var attendance = await _unitOfWork.Attendances
            .GetByIdAsync(request.AttendanceId, cancellationToken);
        if (attendance is null)
        {
            return Result.Failure<IReadOnlyList<AttendanceSessionDto>>(DomainErrors.Attendance.NotFound);
        }

        var logs = await _unitOfWork.AttendanceLogs
            .GetByAttendanceIdAsync(request.AttendanceId, cancellationToken);

        return Result.Success(AttendanceSummaryCalculator.BuildSessions(logs));
    }
}
