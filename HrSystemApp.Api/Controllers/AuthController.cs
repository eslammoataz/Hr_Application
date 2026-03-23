using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using HrSystemApp.Application.DTOs.Auth;
using HrSystemApp.Application.Features.Auth.Commands.ChangePassword;
using HrSystemApp.Application.Features.Auth.Commands.ForceChangePassword;
using HrSystemApp.Application.Features.Auth.Commands.LoginUser;
using HrSystemApp.Application.Features.Auth.Commands.LogoutUser;


namespace HrSystemApp.Api.Controllers;

/// <summary>
/// Authentication - Login, Logout, Change Password
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
        var command = new LoginUserCommand(
            request.Email, 
            request.Password, 
            request.FcmToken, 
            request.DeviceType, 
            request.Language);
        var result = await _sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Logout and invalidate current token</summary>
    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = HttpContext.User.FindFirstValue("sub");
        var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _sender.Send(new LogoutUserCommand(userId, token), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// For standard users who are already logged in and want to update their password.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = HttpContext.User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("ChangePassword attempt without valid UserId in token");
            return Unauthorized();
        }

        var result = await _sender.Send(
            new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword),
            cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Specifically for users who must change their default password upon first login.
    /// </summary>
    [HttpPost("force-change-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForceChangePassword(
        [FromBody] FirstTimeChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ForceChangePasswordCommand(request.UserId, request.CurrentPassword, request.NewPassword),
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
            UserId = HttpContext.User.FindFirstValue("sub"),
            Email = HttpContext.User.FindFirstValue("email"),
            Name = HttpContext.User.FindFirstValue("name"),
            Role = HttpContext.User.FindFirstValue("role"),
            EmployeeId = HttpContext.User.FindFirstValue("employeeId"),
            Phone = HttpContext.User.FindFirstValue("phone")
        });
    }
}
