using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Features.Employees.Commands.CreateEmployee;

namespace HrSystemApp.Api.Controllers;

/// <summary>
/// Employee management — SuperAdmin only
/// </summary>
public class EmployeesController : BaseApiController
{
    private readonly ISender _sender;

    public EmployeesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Create a new employee and their login account (SuperAdmin only).
    /// The employee's temporary password is their phone number and must be changed on first login.
    /// </summary>
    [HttpPost]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(CreateEmployeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateEmployee(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateEmployeeCommand(
            request.FullName,
            request.Email,
            request.PhoneNumber,
            request.CompanyId,
            request.Role);

        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }
}
