using HrSystemApp.Application.Common;
using HrSystemApp.Application.Features.Admin.Commands.UpdateEmployeeBalance;
using HrSystemApp.Application.Features.Admin.Commands.InitializeYearlyBalances;
using HrSystemApp.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HrSystemApp.Api.Authorization;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Roles.ExecutiveOrAbove)]
[Route("api/admin")]
public class AdminManagementController : BaseApiController
{
    private readonly ISender _sender;

    public AdminManagementController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Update or Initialize an employee's leave balance manually (Admin/HR only)
    /// </summary>
    [HttpPut("employees/{employeeId}/leave-balances")]
    public async Task<IActionResult> UpdateBalance(Guid employeeId, [FromBody] UpdateBalanceRequest request)
    {
        var command = new UpdateEmployeeBalanceCommand(
            employeeId,
            request.LeaveType,
            request.Year,
            request.TotalDays);

        return HandleResult(await _sender.Send(command));
    }

    /// <summary>
    /// Initialize Yearly Balances for ALL active employees (Admin/HR only)
    /// </summary>
    [HttpPost("initialize-leave-year/{year}")]
    public async Task<IActionResult> InitializeYear(int year)
    {
        return HandleResult(await _sender.Send(new InitializeYearlyBalancesCommand(year)));
    }
}

public record UpdateBalanceRequest(LeaveType LeaveType, int Year, decimal TotalDays);
