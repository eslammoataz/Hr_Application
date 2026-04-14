using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.DTOs.Departments;
using HrSystemApp.Application.Features.Departments.Commands.CreateDepartment;
using HrSystemApp.Application.Features.Departments.Commands.DeleteDepartment;
using HrSystemApp.Application.Features.Departments.Commands.UpdateDepartment;
using HrSystemApp.Application.Features.Departments.Queries.GetDepartmentById;
using HrSystemApp.Application.Features.Departments.Queries.GetDepartments;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class DepartmentsController : BaseApiController
{
    private readonly ISender _sender;

    public DepartmentsController(ISender sender) => _sender = sender;

    /// <summary>Get all departments for a company.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Viewers)]
    public async Task<IActionResult> GetAll([FromQuery] Guid companyId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetDepartmentsQuery(companyId), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Get a department by ID, including its units.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Viewers)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetDepartmentByIdQuery(id), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Create a new department.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.HierarchyManagers)]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateDepartmentCommand(
            request.CompanyId, request.Name, request.Description,
            request.VicePresidentId, request.ManagerId);
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Update an existing department.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.HierarchyManagers)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateDepartmentCommand(
            id, request.Name, request.Description,
            request.VicePresidentId, request.ManagerId);
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Soft-delete a department.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.ExecutiveOrAbove)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteDepartmentCommand(id), cancellationToken);
        return HandleResult(result);
    }
}
