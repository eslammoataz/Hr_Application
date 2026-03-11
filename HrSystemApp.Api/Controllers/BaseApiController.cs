using HrSystemApp.Application.Common;
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
            return result.Value is null ? NoContent() : Ok(result.Value);
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
        return error.Code switch
        {
            "Auth.InvalidCredentials" or "Auth.InvalidOtp" => Unauthorized(new { error.Code, error.Message }),
            "Auth.Unauthorized" => Forbid(),
            var code when code.Contains("NotFound") => NotFound(new { error.Code, error.Message }),
            var code when code.Contains("Validation") => BadRequest(new { error.Code, error.Message }),
            _ => BadRequest(new { error.Code, error.Message })
        };
    }
}
