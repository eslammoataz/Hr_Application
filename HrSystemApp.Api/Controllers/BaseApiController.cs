using HrSystemApp.Application.Common;
using HrSystemApp.Application.Resources;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

/// <summary>
/// Base API controller with common functionality
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Handle Result pattern and return appropriate HTTP response
    /// </summary>
    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            var successResponse = new ApiResponse<T>(true, result.Value);
            return result.Value is null ? NoContent() : Ok(successResponse);
        }

        return HandleError(result.Error);
    }

    /// <summary>
    /// Handle Result pattern (no value) and return appropriate HTTP response
    /// </summary>
    protected IActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return HandleError(result.Error);
    }

    /// <summary>
    /// Map error to HTTP status code
    /// </summary>
    private IActionResult HandleError(Error error)
    {
        var localizer = HttpContext.RequestServices.GetRequiredService<IErrorLocalizer>();
        var localizedError = localizer.Localize(error);
        var errorResponse = new ApiResponse<object>(false, null, localizedError);

        return localizedError.Code switch
        {
            "Auth.InvalidCredentials" or "Auth.InvalidOtp" => Unauthorized(errorResponse),
            "Auth.Unauthorized" => Unauthorized(errorResponse),
            "General.Forbidden" or "Auth.Forbidden" => Forbid(),
            var code when code.Contains("NotFound") => NotFound(errorResponse),
            _ => BadRequest(errorResponse)
        };
    }
}
