using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.DTOs.LeaveBalances;
using HrSystemApp.Application.Features.LeaveBalances.Commands.AdjustLeaveBalance;
using HrSystemApp.Application.Features.LeaveBalances.Commands.InitializeLeaveBalance;
using HrSystemApp.Application.Features.LeaveBalances.Queries.GetLeaveBalance;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class LeaveBalancesController : BaseApiController
{
    private readonly ISender _sender;

    public LeaveBalancesController(ISender sender) => _sender = sender;

    /// <summary>Get leave balances for an employee for a given year.</summary>
    [HttpGet("{employeeId:guid}")]
    [Authorize(Roles = Roles.Viewers)]
    public async Task<IActionResult> GetBalance(Guid employeeId, [FromQuery] int? year, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetLeaveBalanceQuery(employeeId, year ?? DateTime.UtcNow.Year), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Get my own leave balance (any authenticated employee).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyBalance([FromQuery] int? year, CancellationToken cancellationToken)
    {
        var employeeIdClaim = User.FindFirstValue("employeeId");
        if (!Guid.TryParse(employeeIdClaim, out var employeeId)) return Unauthorized();

        var result = await _sender.Send(
            new GetLeaveBalanceQuery(employeeId, year ?? DateTime.UtcNow.Year), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Initialize leave balance for an employee.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.HierarchyManagers)]
    public async Task<IActionResult> Initialize([FromBody] InitializeLeaveBalanceRequest request, CancellationToken cancellationToken)
    {
        var command = new InitializeLeaveBalanceCommand(
            request.EmployeeId, request.LeaveType, request.Year, request.TotalDays);
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Adjust leave balance (HR override).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.HierarchyManagers)]
    public async Task<IActionResult> Adjust(Guid id, [FromBody] AdjustLeaveBalanceRequest request, CancellationToken cancellationToken)
    {
        var command = new AdjustLeaveBalanceCommand(id, request.NewTotalDays, request.UsedDays);
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }
}
