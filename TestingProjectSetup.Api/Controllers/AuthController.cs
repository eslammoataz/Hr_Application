using TestingProjectSetup.Application.DTOs.Auth;
using TestingProjectSetup.Application.Features.Auth.Commands.RegisterUser;
using TestingProjectSetup.Application.Features.Auth.Commands.LoginUser;
using TestingProjectSetup.Application.Features.Auth.Commands.LogoutUser;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MediatR;

namespace TestingProjectSetup.Api.Controllers;

/// <summary>
/// Authentication controller for Email/Password
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

    /// <summary>
    /// Register a new user
    /// </summary>
    /// <param name="request">Registration details</param>
    /// <returns>Authentication token and user info</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Registration requested for email: {Email}", request.Email);

        var command = new RegisterUserCommand(request.Name, request.Email, request.PhoneNumber, request.Password);
        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Login and get authentication token
    /// </summary>
    /// <param name="request">Email and password</param>
    /// <returns>Authentication token and user info</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Login requested for email: {Email}", request.Email);

        var command = new LoginUserCommand(request.Email, request.Password);
        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Logout and invalidate token
    /// </summary>
    /// <returns>Success or error</returns>
    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("sub");
        var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var command = new LogoutUserCommand(userId, token);
        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Get current user info from token
    /// </summary>
    /// <returns>Current user information</returns>
    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirstValue("sub");
        var email = User.FindFirstValue("email");
        var name = User.FindFirstValue("name");
        var phone = User.FindFirstValue("phone");

        return Ok(new
        {
            UserId = userId,
            Email = email,
            Name = name,
            Phone = phone
        });
    }
}
