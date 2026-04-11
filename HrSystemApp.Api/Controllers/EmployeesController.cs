using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.Features.Employees.Commands.AssignEmployeeToTeam;
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
        [FromQuery] Guid? teamId,
        [FromQuery] UserRole? role,
        [FromQuery] EmploymentStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetEmployeesQuery(companyId, teamId, search, role, status, page, pageSize),
            cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves the authenticated user's employee profile.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>An IActionResult containing the user's profile on success; an UnauthorizedResult if the user ID claim is missing; otherwise an appropriate error response.</returns>
    [HttpGet("me/profile")]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(AppClaimTypes.Subject);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _sender.Send(new GetMyProfileQuery(userId), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Gets the current user's leave balances (total, used, remaining).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the request.</param>
    /// <returns>An IActionResult containing the user's leave balances: total, used, and remaining.</returns>
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
            request.CompanyId, request.Role, request.DepartmentId,
            request.UnitId, request.TeamId);
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
            request.DepartmentId, request.UnitId, request.TeamId,
            request.ManagerId, request.MedicalClass, request.ContractEndDate);
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Assign employee to a team.</summary>
    [HttpPut("{id:guid}/assign-team")]
    [Authorize(Roles = Roles.UnitManagers)]
    public async Task<IActionResult> AssignToTeam(Guid id, [FromBody] AssignEmployeeToTeamRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new AssignEmployeeToTeamCommand(id, request.TeamId), cancellationToken);
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

    /// <summary>
    /// Creates a new profile update request for the currently authenticated user.
    /// </summary>
    /// <param name="request">The profile update data submitted by the user.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> representing the command result; returns 401 Unauthorized if the user id claim is missing.
    /// </returns>
    [HttpPost("me/profile-update-requests")]
    public async Task<IActionResult> CreateProfileUpdateRequest([FromBody] CreateProfileUpdateRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(AppClaimTypes.Subject);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _sender.Send(new CreateProfileUpdateRequestCommand(userId, request), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves the authenticated user's profile update requests using optional pagination.
    /// </summary>
    /// <param name="page">Page number to retrieve; defaults to 1.</param>
    /// <param name="pageSize">Number of items per page; defaults to 20.</param>
    /// <returns>An <see cref="IActionResult"/> containing a paginated list of the user's profile update requests on success, or an Unauthorized result if the caller's user id claim is missing.</returns>
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

    /// <summary>
    /// Retrieves profile update requests for HR users, optionally filtered by status and paginated.
    /// </summary>
    /// <param name="status">Optional filter to return only requests with the specified status.</param>
    /// <param name="page">Page number to return (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing a paginated list of profile update requests when successful;
    /// returns 401 Unauthorized if the HR user id claim is missing.
    /// </returns>
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

    /// <summary>
    /// Processes an HR user's decision to approve or reject a profile update request identified by the given id.
    /// </summary>
    /// <param name="id">The identifier of the profile update request to handle.</param>
    /// <param name="request">The HR decision and any accompanying data for handling the request.</param>
    /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> representing the outcome of the operation:
    /// returns <see cref="UnauthorizedResult"/> if the HR user id claim is missing; otherwise returns the result produced by handling the command.
    /// </returns>
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
