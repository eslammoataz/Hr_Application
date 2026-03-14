using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using HrSystemApp.Application.DTOs.Auth;
using HrSystemApp.Application.Features.Auth.Commands.ChangePassword;
using HrSystemApp.Application.Features.Auth.Commands.LoginUser;
using HrSystemApp.Application.Features.Auth.Commands.LogoutUser;

namespace HrSystemApp.Api.Controllers;

/// <summary>
/// Authentication — Login, Logout, Change Password
/// </summary>
public class AuthController : BaseApiController
{
    private readonly ISender _sender;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ISender sender, ILogger<AuthController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>Login with email and password</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Login requested for: {Email}", request.Email);
        var result = await _sender.Send(new LoginUserCommand(request.Email, request.Password), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Logout and invalidate current token</summary>
    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("sub");
        var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _sender.Send(new LogoutUserCommand(userId, token), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Change password (required on first login)</summary>
    [HttpPost("change-password")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _sender.Send(
            new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword),
            cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Get current authenticated user info from token claims</summary>
    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        return Ok(new
        {
            UserId = User.FindFirstValue("sub"),
            Email = User.FindFirstValue("email"),
            Name = User.FindFirstValue("name"),
            Role = User.FindFirstValue("role"),
            EmployeeId = User.FindFirstValue("employeeId"),
            Phone = User.FindFirstValue("phone")
        });
    }
}
