using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Features.Companies.Commands.CreateCompany;
using HrSystemApp.Application.Features.Companies.Commands.CreateCompanyLocation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CompaniesController : BaseApiController
{
    private readonly ISender _sender;

    public CompaniesController(ISender sender) => _sender = sender;

    /// <summary>Create a new company.</summary>
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
            request.YearlyVacationDays);

        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Add a location to a company.</summary>
    [HttpPost("{companyId:guid}/locations")]
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
}
