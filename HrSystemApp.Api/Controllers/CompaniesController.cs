using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Features.Companies.Commands.CreateCompany;
using HrSystemApp.Application.Features.Companies.Commands.CreateCompanyLocation;

namespace HrSystemApp.Api.Controllers;

/// <summary>
/// Company management — SuperAdmin only
/// </summary>
public class CompaniesController : BaseApiController
{
    private readonly ISender _sender;

    public CompaniesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Create a new company (SuperAdmin only)</summary>
    [HttpPost]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "SuperAdmin")]
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

    /// <summary>Add a location to a company (SuperAdmin only)</summary>
    [HttpPost("{companyId:guid}/locations")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "SuperAdmin")]
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
}
