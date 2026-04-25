using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.Features.RequestTypes.Commands;
using HrSystemApp.Application.Features.RequestTypes.Queries;
using HrSystemApp.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

[ApiController]
[Route("api/request-types")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class RequestTypesController : BaseApiController
{
    private readonly ISender _sender;

    public RequestTypesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get all request types available for the company (system types + custom types).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetRequestTypesQuery query)
    {
        return HandleResult(await _sender.Send(query));
    }

    /// <summary>
    /// Get a specific request type by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        return HandleResult(await _sender.Send(new GetRequestTypeByIdQuery(id)));
    }

    /// <summary>
    /// Create a new custom request type for the company.
    /// </summary>
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Roles.ExecutiveOrAbove)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequestTypeCommand command)
    {
        return HandleResult(await _sender.Send(command));
    }

    /// <summary>
    /// Update a custom request type. System types cannot be modified.
    /// </summary>
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Roles.ExecutiveOrAbove)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRequestTypeCommand command)
    {
        command.Id = id;
        return HandleResult(await _sender.Send(command));
    }

    /// <summary>
    /// Delete (soft delete) a custom request type. System types cannot be deleted.
    /// </summary>
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Roles.ExecutiveOrAbove)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        return HandleResult(await _sender.Send(new DeleteRequestTypeCommand(id)));
    }
}
