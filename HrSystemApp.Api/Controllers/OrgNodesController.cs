using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.DTOs.OrgNodes;
using HrSystemApp.Application.Features.OrgNodes.Commands.BulkSetupOrgNodes;
using HrSystemApp.Application.Features.OrgNodes.Commands.CreateOrgNode;
using HrSystemApp.Application.Features.OrgNodes.Commands.DeleteOrgNode;
using HrSystemApp.Application.Features.OrgNodes.Commands.UpdateOrgNode;
using HrSystemApp.Application.Features.OrgNodes.Commands.AssignEmployeeToNode;
using HrSystemApp.Application.Features.OrgNodes.Commands.UnassignEmployeeFromNode;
using HrSystemApp.Application.Features.OrgNodes.Queries.GetOrgNodeTree;
using HrSystemApp.Application.Features.OrgNodes.Queries.GetOrgNodeDetails;
using HrSystemApp.Application.Features.OrgNodes.Queries.GetMyCompanyHierarchy;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/orgnodes")]
public class OrgNodesController : BaseApiController
{
    private readonly ISender _sender;

    public OrgNodesController(ISender sender) => _sender = sender;

    /// <summary>Get org node tree (root nodes if no parentId, or children of parentId).</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Viewers)]
    public async Task<IActionResult> GetTree([FromQuery] GetOrgNodeTreeQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Get full details of a specific node.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Viewers)]
    public async Task<IActionResult> GetDetails(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetOrgNodeDetailsQuery(id), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Create a new org node.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.HierarchyManagers)]
    public async Task<IActionResult> Create([FromBody] CreateOrgNodeCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Update an existing org node.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.HierarchyManagers)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrgNodeCommand command, CancellationToken cancellationToken)
    {
        command.Id = id;
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Delete an org node (hard delete if leaf, reparent children if has children or assignments).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.HierarchyManagers)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteOrgNodeCommand(id), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Assign an employee to an org node.</summary>
    [HttpPost("{id:guid}/assignments")]
    [Authorize(Roles = Roles.HierarchyManagers)]
    public async Task<IActionResult> AssignEmployee(Guid id, [FromBody] AssignEmployeeToNodeCommand command, CancellationToken cancellationToken)
    {
        command.OrgNodeId = id;
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Unassign an employee from an org node.</summary>
    [HttpDelete("{id:guid}/assignments/{employeeId:guid}")]
    [Authorize(Roles = Roles.HierarchyManagers)]
    public async Task<IActionResult> UnassignEmployee(Guid id, Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UnassignEmployeeFromNodeCommand(id, employeeId), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Bulk setup org nodes and assignments in a single transaction.</summary>
    [HttpPost("bulk-setup")]
    [Authorize(Roles = Roles.HierarchyManagers)]
    public async Task<IActionResult> BulkSetup([FromBody] BulkSetupOrgNodesCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Get the full hierarchy tree for the logged-in user's company.</summary>
    [HttpGet("my-company")]
    [Authorize(Roles = Roles.Viewers)]
    public async Task<IActionResult> GetMyCompanyHierarchy([FromQuery] GetMyCompanyHierarchyQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return HandleResult(result);
    }
}