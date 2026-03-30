using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs;
using HrSystemApp.Application.Features.Requests.Commands.ApproveRequest;
using HrSystemApp.Application.Features.Requests.Commands.CreateRequest;
using HrSystemApp.Application.Features.Requests.Commands.DeleteRequest;
using HrSystemApp.Application.Features.Requests.Commands.RejectRequest;
using HrSystemApp.Application.Features.Requests.Commands.UpdateRequest;
using HrSystemApp.Application.Features.Requests.Queries.GetPendingApprovals;
using HrSystemApp.Application.Features.Requests.Queries.GetCompanyRequests;
using HrSystemApp.Application.Features.Requests.Queries.GetRequestById;
using HrSystemApp.Application.Features.Requests.Queries.GetUserRequests;
using HrSystemApp.Application.Features.Requests.Queries.GetRequestTypes;
using HrSystemApp.Application.Features.Requests.Queries.GetMyLeaveBalances;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/Employees/requests")]
public class RequestsController : BaseApiController
{
    private readonly ISender _sender;

    public RequestsController(ISender sender)
    {
        _sender = sender;
    }

    // --- Employee Context (Self Service) ---

    /// <summary>
    /// Create a new Request (Dynamic JSON)
    /// </summary>
    [HttpPost("me")]
    public async Task<IActionResult> Create(CreateRequestCommand command)
    {
        return HandleResult(await _sender.Send(command));
    }

    /// <summary>
    /// Get current user's request history
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyRequests([FromQuery] GetUserRequestsQuery query)
    {
        return HandleResult(await _sender.Send(query));
    }

    /// <summary>
    /// Get current user's active leave balances (current year)
    /// </summary>
    [HttpGet("me/balances")]
    public async Task<IActionResult> GetMyBalances()
    {
        return HandleResult(await _sender.Send(new GetMyLeaveBalancesQuery()));
    }

    /// <summary>
    /// Get request details of my own request
    /// </summary>
    [HttpGet("me/{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        return HandleResult(await _sender.Send(new GetRequestByIdQuery(id)));
    }

    /// <summary>
    /// Update a pending request (only if no actions taken yet)
    /// </summary>
    [HttpPut("me/{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateRequestCommand command)
    {
        if (id != command.Id) return BadRequest("Mismatched ID.");
        return HandleResult(await _sender.Send(command));
    }

    /// <summary>
    /// Delete a pending request (only if no approvals yet)
    /// </summary>
    [HttpDelete("me/{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        return HandleResult(await _sender.Send(new DeleteRequestCommand(id)));
    }


    // --- Approver Context (Tasks) ---

    /// <summary>
    /// Get requests pending for MY approval (For Managers/Approvers)
    /// </summary>
    [HttpGet("approvals/pending")]
    public async Task<IActionResult> GetPendingApprovals([FromQuery] GetPendingApprovalsQuery query)
    {
        return HandleResult(await _sender.Send(query));
    }

    /// <summary>
    /// Approve a request
    /// </summary>
    [HttpPost("approvals/{id}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] EvaluationRequest request)
    {
        return HandleResult(await _sender.Send(new ApproveRequestCommand(id, request.Comment)));
    }

    /// <summary>
    /// Reject a request
    /// </summary>
    [HttpPost("approvals/{id}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] EvaluationRequest request)
    {
        return HandleResult(await _sender.Send(new RejectRequestCommand(id, request.Comment ?? "No reason provided.")));
    }


    // --- Admin Context (Oversight) ---

    /// <summary>
    /// Get ALL requests for the company (For Company Admins/Oversight)
    /// </summary>
    [HttpGet("admin/company-wide")]
    public async Task<IActionResult> GetCompanyWideRequests([FromQuery] GetCompanyRequestsQuery query)
    {
        return HandleResult(await _sender.Send(query));
    }
}
