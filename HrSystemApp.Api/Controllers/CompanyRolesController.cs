using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.Features.Roles.Commands.AssignRoleToEmployee;
using HrSystemApp.Application.Features.Roles.Commands.CreateCompanyRole;
using HrSystemApp.Application.Features.Roles.Commands.DeleteCompanyRole;
using HrSystemApp.Application.Features.Roles.Commands.RemoveRoleFromEmployee;
using HrSystemApp.Application.Features.Roles.Commands.UpdateCompanyRole;
using HrSystemApp.Application.Features.Roles.Queries.GetCompanyRoleById;
using HrSystemApp.Application.Features.Roles.Queries.GetCompanyRoles;
using HrSystemApp.Application.Features.Roles.Queries.GetEmployeeRoles;
using HrSystemApp.Application.Features.Roles.Queries.GetEmployeesByRole;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Authorize(Roles = Roles.HrOrAbove)]
[Route("api/company-roles")]
public class CompanyRolesController : BaseApiController
{
    private readonly ISender _sender;

    public CompanyRolesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(new GetCompanyRolesQuery(), cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(new GetCompanyRoleByIdQuery(id), cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCompanyRoleRequest request, CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(
            new CreateCompanyRoleCommand(request.Name, request.Description, request.Permissions),
            cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateCompanyRoleRequest request, CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(
            new UpdateCompanyRoleCommand(id, request.Name, request.Description, request.Permissions),
            cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(new DeleteCompanyRoleCommand(id), cancellationToken));
    }

    [HttpGet("{roleId:guid}/employees")]
    public async Task<IActionResult> GetRoleEmployees(
        Guid roleId, CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(new GetEmployeesByRoleQuery(roleId), cancellationToken));
    }

    [HttpPost("{roleId:guid}/employees/{employeeId:guid}")]
    public async Task<IActionResult> AssignToEmployee(
        Guid roleId, Guid employeeId, CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(
            new AssignRoleToEmployeeCommand(employeeId, roleId),
            cancellationToken));
    }

    [HttpDelete("{roleId:guid}/employees/{employeeId:guid}")]
    public async Task<IActionResult> RemoveFromEmployee(
        Guid roleId, Guid employeeId, CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(
            new RemoveRoleFromEmployeeCommand(employeeId, roleId),
            cancellationToken));
    }

    [HttpGet("by-employee/{employeeId:guid}")]
    public async Task<IActionResult> GetByEmployee(Guid employeeId, CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(
            new GetEmployeeRolesQuery(employeeId), cancellationToken));
    }
}

public sealed record CreateCompanyRoleRequest(
    string Name,
    string? Description,
    IReadOnlyList<string>? Permissions);

public sealed record UpdateCompanyRoleRequest(
    string Name,
    string? Description,
    IReadOnlyList<string>? Permissions);
