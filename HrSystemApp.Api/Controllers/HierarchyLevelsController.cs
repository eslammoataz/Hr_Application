using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.Features.HierarchyLevels.Commands.CreateHierarchyLevel;
using HrSystemApp.Application.Features.HierarchyLevels.Commands.UpdateHierarchyLevel;
using HrSystemApp.Application.Features.HierarchyLevels.Commands.DeleteHierarchyLevel;
using HrSystemApp.Application.Features.HierarchyLevels.Queries.GetHierarchyLevels;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/hierarchy-levels")]
public class HierarchyLevelsController : BaseApiController
{
    private readonly ISender _sender;

    public HierarchyLevelsController(ISender sender) => _sender = sender;

    /// <summary>Get all hierarchy levels ordered by sort order.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Viewers)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetHierarchyLevelsQuery(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Create a new hierarchy level.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.HierarchyManagers)]
    public async Task<IActionResult> Create([FromBody] CreateHierarchyLevelCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Update an existing hierarchy level.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.HierarchyManagers)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateHierarchyLevelCommand command, CancellationToken cancellationToken)
    {
        command.Id = id;
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Delete a hierarchy level (fails if nodes are assigned to it).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.HierarchyManagers)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteHierarchyLevelCommand(id), cancellationToken);
        return HandleResult(result);
    }
}