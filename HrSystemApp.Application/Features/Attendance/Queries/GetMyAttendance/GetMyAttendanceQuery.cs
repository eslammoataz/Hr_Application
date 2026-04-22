using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Attendance;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Attendance.Common;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Attendance.Queries.GetMyAttendance;

public sealed record GetMyAttendanceQuery(
    DateOnly? FromDate,
    DateOnly? ToDate,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<AttendanceSummaryResponse>>>;

public class GetMyAttendanceQueryHandler
    : IRequestHandler<GetMyAttendanceQuery, Result<PagedResult<AttendanceSummaryResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetMyAttendanceQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedResult<AttendanceSummaryResponse>>> Handle(
        GetMyAttendanceQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result.Failure<PagedResult<AttendanceSummaryResponse>>(DomainErrors.Auth.Unauthorized);
        }

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee is null)
        {
            return Result.Failure<PagedResult<AttendanceSummaryResponse>>(DomainErrors.Employee.NotFound);
        }

        var toDate = request.ToDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = request.FromDate ?? toDate.AddDays(-30);
        if (fromDate > toDate)
        {
            return Result.Failure<PagedResult<AttendanceSummaryResponse>>(DomainErrors.General.ArgumentError);
        }

        var paged = await _unitOfWork.Attendances.GetMyAttendancePagedAsync(
            employee.Id,
            fromDate,
            toDate,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var items = paged.Items.Select(a => new AttendanceSummaryResponse(
            a.EmployeeId,
            employee.FullName,
            a.Date,
            a.FirstClockInUtc,
            a.LastClockOutUtc,
            a.TotalHours,
            a.Status.ToString(),
            a.IsLate,
            a.IsEarlyLeave,
            a.Reason,
            AttendanceSummaryCalculator.BuildSessions(
                a.Logs.OrderBy(l => l.TimestampUtc).ToList()))).ToList();

        return Result.Success(PagedResult<AttendanceSummaryResponse>.Create(
            items, request.PageNumber, request.PageSize, paged.TotalCount));
    }
}
