using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Features.Companies.Commands.CreateCompany;
using HrSystemApp.Application.Features.Companies.Commands.CreateCompanyLocation;
using HrSystemApp.Application.Features.Companies.Commands.UpdateCompany;
using HrSystemApp.Application.Features.Companies.Commands.ChangeCompanyStatus;
using HrSystemApp.Application.Features.Companies.Queries.GetCompanies;
using HrSystemApp.Application.Features.Companies.Queries.GetCompanyById;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/companies")]
public class CompaniesController : BaseApiController
{
    private readonly ISender _sender;

    public CompaniesController(ISender sender) => _sender = sender;

    /// <summary>Create a new company.</summary>
    [HttpPost("superadmin")]
    [Authorize(Roles = Roles.SuperAdminOnly)]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateCompany(
        [FromBody] CreateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCompanyCommand(
            request.CompanyName,
            request.CompanyLogoUrl,
            request.YearlyVacationDays);

        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Add a location to a company.</summary>
    [HttpPost("superadmin/{companyId:guid}/locations")]
    [Authorize(Roles = Roles.SuperAdminOnly)]
    [ProducesResponseType(typeof(CompanyLocationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateLocation(
        Guid companyId,
        [FromBody] CreateCompanyLocationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCompanyLocationCommand(
            companyId,
            request.LocationName,
            request.Address,
            request.Latitude,
            request.Longitude);

        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Get all companies.</summary>
    [HttpGet("superadmin")]
    [Authorize(Roles = Roles.SuperAdminOnly)]
    [ProducesResponseType(typeof(PagedResult<CompanyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? searchTerm,
        [FromQuery] CompanyStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeLocations = false,
        [FromQuery] bool includeDepartments = false,
        CancellationToken cancellationToken = default)
    {
        var result =
            await _sender.Send(
                new GetCompaniesQuery(searchTerm, status, pageNumber, pageSize, includeLocations, includeDepartments),
                cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Get a company by ID.</summary>
    [HttpGet("superadmin/{id:guid}")]
    [Authorize(Roles = Roles.SuperAdminOnly)]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromQuery] bool includeLocations = false,
        [FromQuery] bool includeDepartments = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetCompanyByIdQuery(id, includeLocations, includeDepartments),
            cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Update an existing company.</summary>
    [HttpPut("superadmin/{id:guid}")]
    [Authorize(Roles = Roles.SuperAdminOnly)]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCompanyCommand(
            id,
            request.CompanyName,
            request.CompanyLogoUrl,
            request.YearlyVacationDays,
            request.Status);

        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Change the status of an existing company.</summary>
    [HttpPatch("superadmin/{id:guid}/status")]
    [Authorize(Roles = Roles.SuperAdminOnly)]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        [FromBody] ChangeCompanyStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ChangeCompanyStatusCommand(id, request.Status);
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }
}