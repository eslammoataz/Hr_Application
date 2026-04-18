using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.Features.Employees.Commands.ChangeEmployeeStatus;
using HrSystemApp.Application.Features.Employees.Commands.CreateEmployee;
using HrSystemApp.Application.Features.Employees.Commands.UpdateEmployee;
using HrSystemApp.Application.Features.Employees.Queries.GetEmployeeById;
using HrSystemApp.Application.Features.Employees.Queries.GetEmployees;
using HrSystemApp.Application.Features.Employees.Queries.GetMyProfile;
using HrSystemApp.Application.Features.ProfileUpdateRequests.Commands.CreateProfileUpdateRequest;
using HrSystemApp.Application.Features.ProfileUpdateRequests.Commands.HandleProfileUpdateRequest;
using HrSystemApp.Application.Features.ProfileUpdateRequests.Queries.GetAllProfileUpdateRequests;
using HrSystemApp.Application.Features.ProfileUpdateRequests.Queries.GetMyProfileUpdateRequests;
using HrSystemApp.Application.Features.Requests.Queries.GetMyLeaveBalances;
using HrSystemApp.Application.DTOs.Employees.ProfileUpdateRequests;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using HrSystemApp.Domain.Constants;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class EmployeesController : BaseApiController
{
    private readonly ISender _sender;

    public EmployeesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Get all employees (paginated).</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Viewers)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? companyId,
        [FromQuery] UserRole? role,
        [FromQuery] EmploymentStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetEmployeesQuery(companyId, search, role, status, page, pageSize),
            cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Get my own employee profile (any authenticated user).</summary>
    [HttpGet("me/profile")]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(AppClaimTypes.Subject);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _sender.Send(new GetMyProfileQuery(userId), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Get my own leave balance (Total, Used, Remaining).</summary>
    [HttpGet("me/balances")]
    public async Task<IActionResult> GetMyBalances(CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(new GetMyLeaveBalancesQuery(), cancellationToken));
    }

    /// <summary>Get employee by ID.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Viewers)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetEmployeeByIdQuery(id), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Create a new employee and login account.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.HierarchyManagers)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateEmployeeCommand(
            request.FullName, request.Email, request.PhoneNumber,
            request.CompanyId, request.Role);
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Update employee details.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.HierarchyManagers)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateEmployeeCommand(
            id, request.FullName, request.PhoneNumber, request.Address,
            request.ManagerId, request.MedicalClass, request.ContractEndDate);
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Change employee employment status.</summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = Roles.HierarchyManagers)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        [FromBody] ChangeEmployeeStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ChangeEmployeeStatusCommand(id, request.Status), cancellationToken);
        return HandleResult(result);
    }

    // ── Profile Update Requests (Employee) ──────────────────────────────

    /// <summary>Submit a new profile update request.</summary>
    [HttpPost("me/profile-update-requests")]
    public async Task<IActionResult> CreateProfileUpdateRequest([FromBody] CreateProfileUpdateRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(AppClaimTypes.Subject);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _sender.Send(new CreateProfileUpdateRequestCommand(userId, request), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Get my own profile update requests.</summary>
    [HttpGet("me/profile-update-requests")]
    public async Task<IActionResult> GetMyProfileUpdateRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(AppClaimTypes.Subject);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _sender.Send(new GetMyProfileUpdateRequestsQuery(userId, page, pageSize), cancellationToken);
        return HandleResult(result);
    }

    // ── Profile Update Requests (HR) ────────────────────────────────────

    /// <summary>Get all profile update requests (HR only).</summary>
    [HttpGet("profile-update-requests")]
    [Authorize(Roles = Roles.HR)]
    public async Task<IActionResult> GetAllProfileUpdateRequests(
        [FromQuery] ProfileUpdateRequestStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var hrUserId = User.FindFirstValue(AppClaimTypes.Subject);
        if (string.IsNullOrEmpty(hrUserId)) return Unauthorized();

        var result = await _sender.Send(new GetAllProfileUpdateRequestsQuery(hrUserId, status, page, pageSize),
            cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Handle a profile update request (HR only).</summary>
    [HttpPatch("profile-update-requests/{id:guid}/handle")]
    [Authorize(Roles = Roles.HR)]
    public async Task<IActionResult> HandleProfileUpdateRequest(Guid id,
        [FromBody] HandleProfileUpdateRequestDto request, CancellationToken cancellationToken)
    {
        var hrUserId = User.FindFirstValue(AppClaimTypes.Subject);
        if (string.IsNullOrEmpty(hrUserId)) return Unauthorized();

        var result = await _sender.Send(new HandleProfileUpdateRequestCommand(id, hrUserId, request),
            cancellationToken);
        return HandleResult(result);
    }
}
