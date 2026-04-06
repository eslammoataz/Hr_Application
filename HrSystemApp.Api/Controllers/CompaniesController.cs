using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Features.Companies.Commands.CreateCompany;
using HrSystemApp.Application.Features.Companies.Commands.CreateCompanyLocation;
using HrSystemApp.Application.Features.Companies.Commands.DeleteCompanyLocation;
using HrSystemApp.Application.Features.Companies.Commands.UpdateCompany;
using HrSystemApp.Application.Features.Companies.Commands.ChangeCompanyStatus;
using HrSystemApp.Application.Features.Companies.Queries.GetCompanies;
using HrSystemApp.Application.Features.Companies.Queries.GetCompanyById;
using HrSystemApp.Application.Features.Companies.Queries.GetCompanyLocations;
using HrSystemApp.Application.Features.Companies.Queries.GetMyCompany;
using HrSystemApp.Application.Features.Companies.Commands.UpdateMyCompany;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HrSystemApp.Application.Features.Hierarchy.Commands.ConfigureHierarchyPositions;
using HrSystemApp.Application.Features.Hierarchy.Queries.GetCompanyHierarchy;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/companies")]
public class CompaniesController : BaseApiController
{
    private readonly ISender _sender;

    public CompaniesController(ISender sender) => _sender = sender;

    /// <summary>Create a new company. (SuperAdmin only)</summary>
    [HttpPost]
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
            request.YearlyVacationDays,
            request.StartTime,
            request.EndTime,
            request.GraceMinutes,
            request.TimeZoneId);

        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Get all companies. (SuperAdmin only)</summary>
    [HttpGet]
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
    [HttpGet("{id:guid}")]
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
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.CompanyAdmins)]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
            request.StartTime,
            request.EndTime,
            request.GraceMinutes,
            request.TimeZoneId);

        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Get the company of the currently logged in user.</summary>
    [HttpGet("me")]
    [Authorize(Roles = Roles.CompanyAdmins)]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyCompany(
        [FromQuery] bool includeLocations = false,
        [FromQuery] bool includeDepartments = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetMyCompanyQuery(includeLocations, includeDepartments), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Update the company of the currently logged in user.</summary>
    [HttpPut("me")]
    [Authorize(Roles = Roles.CompanyAdmins)]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyCompany(
        [FromBody] UpdateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateMyCompanyCommand(
            request.CompanyName,
            request.CompanyLogoUrl,
            request.YearlyVacationDays,
            request.StartTime,
            request.EndTime,
            request.GraceMinutes,
            request.TimeZoneId);

        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Change the status of an existing company. (SuperAdmin only)</summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = Roles.SuperAdminOnly)]
    [ProducesResponseType(typeof(CompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

    /// <summary>Get all locations for a company.</summary>
    [HttpGet("{companyId:guid}/locations")]
    [Authorize(Roles = Roles.HrOrAbove)]
    [ProducesResponseType(typeof(IReadOnlyList<CompanyLocationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLocations(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var query = new GetCompanyLocationsQuery(companyId);
        var result = await _sender.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Add a location to a company.</summary>
    [HttpPost("{companyId:guid}/locations")]
    [Authorize(Roles = Roles.CompanyAdmins)]
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

    /// <summary>Delete a company location.</summary>
    [HttpDelete("locations/{id:guid}")]
    [Authorize(Roles = Roles.CompanyAdmins)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLocation(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteCompanyLocationCommand(id);
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Gets the full organizational hierarchy for the current user's company.
    /// </summary>
    [HttpGet("hierarchy")]
    [Authorize(Roles = Roles.Viewers)]
    public async Task<IActionResult> GetHierarchy(CancellationToken ct)
    {
        var result = await _sender.Send(new GetCompanyHierarchyQuery(), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Configures the allowed roles and their order in the company hierarchy.
    /// Typically performed by a Company Admin or HR.
    /// </summary>
    [HttpPost("hierarchy/positions")]
    [Authorize(Roles = Roles.CompanyAdmins)]
    public async Task<IActionResult> ConfigurePositions([FromBody] List<HierarchyPositionInputDto> positions,
        CancellationToken ct)
    {
        var result = await _sender.Send(new ConfigureHierarchyPositionsCommand(positions), ct);
        return HandleResult(result);
    }
}
