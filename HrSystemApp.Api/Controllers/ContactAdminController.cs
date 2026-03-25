using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.Features.ContactAdmin.Commands.AcceptContactAdminRequest;
using HrSystemApp.Application.Features.ContactAdmin.Commands.CreateContactAdminRequest;
using HrSystemApp.Application.Features.ContactAdmin.Commands.RejectContactAdminRequest;
using HrSystemApp.Application.Features.ContactAdmin.Queries.GetContactAdminRequests;
using HrSystemApp.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ContactAdminController : BaseApiController
{
    private readonly IMediator _mediator;

    public ContactAdminController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> CreateContactAdminRequest([FromBody] CreateContactAdminRequestCommand command)
    {
        var result = await _mediator.Send(command);
        return HandleResult(result);
    }

    [HttpGet("admin/contact-requests")]
    [Authorize(Roles = Roles.SuperAdminOnly)]
    public async Task<IActionResult> GetContactAdminRequests([FromQuery] GetContactAdminRequestsQuery query)
    {
        var result = await _mediator.Send(query);
        return HandleResult(result);
    }

    [HttpPost("admin/contact-requests/{id:guid}/accept")]
    [Authorize(Roles = Roles.SuperAdminOnly)]
    public async Task<IActionResult> AcceptContactAdminRequest(Guid id)
    {
        var result = await _mediator.Send(new AcceptContactAdminRequestCommand(id));
        return HandleResult(result);
    }

    [HttpPost("admin/contact-requests/{id:guid}/reject")]
    [Authorize(Roles = Roles.SuperAdminOnly)]
    public async Task<IActionResult> RejectContactAdminRequest(Guid id)
    {
        var result = await _mediator.Send(new RejectContactAdminRequestCommand(id));
        return HandleResult(result);
    }
}
