using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.DTOs.Teams;
using HrSystemApp.Application.Features.Teams.Commands.CreateTeam;
using HrSystemApp.Application.Features.Teams.Commands.DeleteTeam;
using HrSystemApp.Application.Features.Teams.Commands.UpdateTeam;
using HrSystemApp.Application.Features.Teams.Queries.GetTeamById;
using HrSystemApp.Application.Features.Teams.Queries.GetTeams;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class TeamsController : BaseApiController
{
    private readonly ISender _sender;

    public TeamsController(ISender sender) => _sender = sender;

    /// <summary>Get all teams for a unit.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Viewers)]
    public async Task<IActionResult> GetAll([FromQuery] Guid unitId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTeamsQuery(unitId), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Get a team by ID, including its members.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Viewers)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTeamByIdQuery(id), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Create a new team.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.HrOrAbove)]
    public async Task<IActionResult> Create([FromBody] CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateTeamCommand(
            request.UnitId, request.Name, request.Description, request.TeamLeaderId);
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Update an existing team.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.HrOrAbove)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateTeamCommand(id, request.Name, request.Description, request.TeamLeaderId);
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Soft-delete a team.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.ExecutiveOrAbove)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteTeamCommand(id), cancellationToken);
        return HandleResult(result);
    }
}
