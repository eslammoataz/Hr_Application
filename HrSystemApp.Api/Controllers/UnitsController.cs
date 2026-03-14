using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.DTOs.Units;
using HrSystemApp.Application.Features.Units.Commands.CreateUnit;
using HrSystemApp.Application.Features.Units.Commands.DeleteUnit;
using HrSystemApp.Application.Features.Units.Commands.UpdateUnit;
using HrSystemApp.Application.Features.Units.Queries.GetUnitById;
using HrSystemApp.Application.Features.Units.Queries.GetUnits;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class UnitsController : BaseApiController
{
    private readonly ISender _sender;

    public UnitsController(ISender sender) => _sender = sender;

    /// <summary>Get all units for a department.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Viewers)]
    public async Task<IActionResult> GetAll([FromQuery] Guid departmentId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetUnitsQuery(departmentId), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Get a unit by ID.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Viewers)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetUnitByIdQuery(id), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Create a new unit.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.UnitManagers)]
    public async Task<IActionResult> Create([FromBody] CreateUnitRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateUnitCommand(
            request.DepartmentId, request.Name, request.Description, request.UnitLeaderId);
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Update an existing unit.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.UnitManagers)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUnitRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateUnitCommand(id, request.Name, request.Description, request.UnitLeaderId);
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Soft-delete a unit.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.CeoOrAbove)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteUnitCommand(id), cancellationToken);
        return HandleResult(result);
    }
}
