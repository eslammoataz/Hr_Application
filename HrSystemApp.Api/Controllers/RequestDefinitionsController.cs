using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.Features.Requests.Commands.Admin;
using HrSystemApp.Application.Features.Requests.Queries.GetRequestDefinitions;
using HrSystemApp.Application.Features.Requests.Queries.GetRequestTypes;
using HrSystemApp.Application.Features.Requests.Queries.PreviewApprovalChain;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

/// <summary>
/// Workflow Setup Controller (Admin Only)
/// </summary>
// [Authorize(Roles = "SuperAdmin,CompanyAdmin")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
// [Authorize(Roles = Roles.CompanyAdmins)]
public class RequestDefinitionsController : BaseApiController
{
    private readonly ISender _sender;

    public RequestDefinitionsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Create a new workflow definition for a request type
    /// </summary>
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Roles.ExecutiveOrAbove)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateRequestDefinitionCommand command)
    {
        return HandleResult(await _sender.Send(command));
    }

    /// <summary>
    /// Update an existing workflow definition
    /// </summary>
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Roles.ExecutiveOrAbove)]

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateRequestDefinitionCommand command)
    {
        command.Id = id;
        return HandleResult(await _sender.Send(command));
    }

    /// <summary>
    /// Delete a workflow definition
    /// </summary>
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Roles.ExecutiveOrAbove)]

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        return HandleResult(await _sender.Send(new DeleteRequestDefinitionCommand(id)));
    }

    /// <summary>
    /// Get all workflow definitions for the company
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GetRequestDefinitionsQuery query)
    {
        return HandleResult(await _sender.Send(query));
    }

    /// <summary>
    /// Get all available request types (Enum)
    /// </summary>
    [HttpGet("types")]
    [AllowAnonymous] // Usually safe to expose names/ids to client
    public async Task<IActionResult> GetTypes()
    {
        return HandleResult(await _sender.Send(new GetRequestTypesQuery()));
    }

    /// <summary>
    /// Get Schema definitions for all request types (from RequestSchemas.json).
    /// Note: This returns schemas for system types only. For custom types, use /api/request-types/{id}/schemas.
    /// </summary>
    [HttpGet("schemas")]
    [AllowAnonymous]
    public IActionResult GetSchemas([FromServices] IRequestSchemaValidator validator, [FromServices] ISender sender)
    {
        // Get system request types from the enum (for backward compatibility during migration)
        var typeKeys = Enum.GetNames<HrSystemApp.Domain.Enums.RequestType>();
        var schemas = typeKeys.ToDictionary(t => t, t => validator.GetSchema(t));
        return Ok(schemas);
    }
}
