using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.Features.Attendance.Commands.BatchOverrideClockOut;
using HrSystemApp.Application.Features.Attendance.Commands.ClockIn;
using HrSystemApp.Application.Features.Attendance.Commands.ClockOut;
using HrSystemApp.Application.Features.Attendance.Commands.OverrideClockOut;
using HrSystemApp.Application.Features.Attendance.Queries.GetCompanyAttendance;
using HrSystemApp.Application.Features.Attendance.Queries.GetMyAttendance;
using HrSystemApp.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/attendance")]
public class AttendanceController : BaseApiController
{
    private readonly ISender _sender;

    public AttendanceController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("clock-in")]
    public async Task<IActionResult> ClockIn([FromBody] ClockActionRequest? request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ClockInCommand(request?.TimestampUtc), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("clock-out")]
    public async Task<IActionResult> ClockOut([FromBody] ClockActionRequest? request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ClockOutCommand(request?.TimestampUtc), cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyAttendance(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetMyAttendanceQuery(fromDate, toDate, pageNumber, pageSize),
            cancellationToken);
        return HandleResult(result);
    }

    [HttpGet]
    [Authorize(Roles = Roles.HrOrAbove)]
    public async Task<IActionResult> GetCompanyAttendance(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] Guid? employeeId,
        [FromQuery] AttendanceStatus? status,
        [FromQuery] bool? isLate,
        [FromQuery] bool? isEarlyLeave,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetCompanyAttendanceQuery(fromDate, toDate, employeeId, status, isLate, isEarlyLeave, pageNumber, pageSize),
            cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("admin/override-clock-out")]
    [Authorize(Roles = Roles.HrOrAbove)]
    public async Task<IActionResult> OverrideClockOut([FromBody] OverrideClockOutRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new OverrideClockOutCommand(request.EmployeeId, request.Date, request.ClockOutUtc, request.Reason),
            cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("admin/override-clock-out/batch")]
    [Authorize(Roles = Roles.HrOrAbove)]
    public async Task<IActionResult> BatchOverrideClockOut([FromBody] BatchOverrideClockOutRequest request, CancellationToken cancellationToken)
    {
        var items = request.Items
            .Select(x => new BatchOverrideItem(x.EmployeeId, x.Date, x.ClockOutUtc, x.Reason))
            .ToList();

        var result = await _sender.Send(new BatchOverrideClockOutCommand(items), cancellationToken);
        return HandleResult(result);
    }
}

public sealed record ClockActionRequest(DateTime? TimestampUtc);

public sealed record OverrideClockOutRequest(Guid EmployeeId, DateOnly Date, DateTime ClockOutUtc, string Reason);

public sealed record BatchOverrideClockOutRequest(IReadOnlyList<OverrideClockOutRequest> Items);
