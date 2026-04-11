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
using HrSystemApp.Application.Features.Auth.Commands.UpdateFcmToken;
using HrSystemApp.Application.Features.Auth.Commands.UpdateLanguage;
using HrSystemApp.Application.Features.Auth.Commands.ForgotPassword;
using HrSystemApp.Application.Features.Auth.Commands.ResetPassword;
using HrSystemApp.Application.Features.Auth.Commands.VerifyOtp;
using HrSystemApp.Application.Features.Auth.Commands.RefreshToken;
using HrSystemApp.Application.Features.Auth.Commands.RevokeToken;
using HrSystemApp.Application.Features.Auth.Commands.RevokeAllTokens;
using HrSystemApp.Application.Features.Auth.Queries.GetUserTokens;
using HrSystemApp.Domain.Constants;


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
        _logger.LogInformation("User {Email} is attempting to login. Action: {ActionType}", request.Email,
            "LoginAttempt");

        var command = new LoginUserCommand(
            request.Email,
            request.Password,
            request.FcmToken,
            request.DeviceType,
            request.Language,
            HttpContext.Connection.RemoteIpAddress?.ToString());
        var result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation("User {Email} successfully logged in. Action: {ActionType}, UserId: {UserId}",
                request.Email, "LoginSuccess", result.Value.UserId);
        }
        else
        {
            _logger.LogWarning("User {Email} failed to login. Reason: {Error}", request.Email, result.Error.Message);
        }

        return HandleResult(result);
    }

    /// <summary>Logout and invalidate current refresh token</summary>
    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        var userId = HttpContext.User.FindFirstValue(AppClaimTypes.Subject);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result =
            await _sender.Send(
                new LogoutUserCommand(userId, request.RefreshToken, HttpContext.Connection.RemoteIpAddress?.ToString()),
                cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Refresh access token using a refresh token</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result =
            await _sender.Send(
                new RefreshTokenCommand(request.RefreshToken, HttpContext.Connection.RemoteIpAddress?.ToString()),
                cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Revoke a specific refresh token</summary>
    [HttpPost("revoke")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Revoke([FromBody] RevokeTokenRequest request, CancellationToken cancellationToken)
    {
        var result =
            await _sender.Send(
                new RevokeTokenCommand(request.RefreshToken, HttpContext.Connection.RemoteIpAddress?.ToString()),
                cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Revoke all refresh tokens for the authenticated user.
    /// </summary>
    /// <returns>HTTP 204 No Content when successful; HTTP 401 Unauthorized if the user claim is missing.</returns>
    [HttpPost("revoke-all")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeAll(CancellationToken cancellationToken)
    {
        var userId = HttpContext.User.FindFirstValue(AppClaimTypes.Subject);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result =
            await _sender.Send(new RevokeAllTokensCommand(userId, HttpContext.Connection.RemoteIpAddress?.ToString()),
                cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves the active refresh tokens for the authenticated user.
    /// </summary>
    /// <returns>
    /// 200 OK with a list of <see cref="RefreshTokenDto"/> when the user is authenticated; 401 Unauthorized if the user claim is missing.
    /// </returns>
    [HttpGet("tokens")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(List<RefreshTokenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTokens(CancellationToken cancellationToken)
    {
        var userId = HttpContext.User.FindFirstValue(AppClaimTypes.Subject);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _sender.Send(new GetUserTokensQuery(userId), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// For standard users who are already logged in and want to update their password.
    /// <summary>
    /// Changes the authenticated user's password using the provided current and new passwords.
    /// </summary>
    /// <param name="request">A request containing the current password and the new password to set.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    /// An <see cref="AuthResponse"/> with status 200 when the password change succeeds; 
    /// 400 for invalid input; 401 when the request lacks a valid authenticated user.
    /// </returns>
    [HttpPost("change-password")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = HttpContext.User.FindFirstValue(AppClaimTypes.Subject);

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

    /// <summary>
    /// Update FCM notification token for currently authenticated user.
    /// <summary>
    /// Updates the authenticated user's FCM token and device type.
    /// </summary>
    /// <param name="request">Contains the FCM token and the device type to associate with the current user.</param>
    /// <returns>`200 OK` when the update succeeds; `401 Unauthorized` if the user claim identifying the current user is missing.</returns>
    [HttpPost("update-fcm-token")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateFcmToken(
        [FromBody] UpdateFcmTokenRequest request, CancellationToken cancellationToken)
    {
        var userId = HttpContext.User.FindFirstValue(AppClaimTypes.Subject);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _sender.Send(
            new UpdateFcmTokenCommand(userId, request.FcmToken, request.DeviceType),
            cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Update preferred language for currently authenticated user.
    /// <summary>
    /// Updates the authenticated user's preferred language.
    /// </summary>
    /// <param name="request">Request containing the target language code.</param>
    /// <returns>`200 OK` with the command result mapped to an `IActionResult`; `401 Unauthorized` if the user is not authenticated.</returns>
    [HttpPost("update-language")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateLanguage(
        [FromBody] UpdateLanguageRequest request, CancellationToken cancellationToken)
    {
        var userId = HttpContext.User.FindFirstValue(AppClaimTypes.Subject);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _sender.Send(
            new UpdateLanguageCommand(userId, request.Language),
            cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Request a password reset OTP.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ForgotPasswordCommand(request.Email, request.Channel),
            cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Verify reset OTP without changing password.
    /// </summary>
    [HttpPost("verify-otp")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new VerifyOtpCommand(request.Email, request.Otp),
            cancellationToken);

        if (result.IsSuccess && result.Value)
        {
            return NoContent();
        }

        return BadRequest(new
        {
            isSuccess = false,
            data = (object?)null,
            error = new
            {
                code = "User.InvalidOtp",
                message = "Invalid OTP code provided."
            }
        });
    }

    /// <summary>
    /// Reset password using OTP.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ResetPasswordCommand(request.Email, request.Otp, request.NewPassword),
            cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Gets the current authenticated user's information from JWT claims.
    /// </summary>
    /// <returns>
    /// An object with the following properties populated from the current user's claims:
    /// `UserId`, `Email`, `Name`, `Role`, `EmployeeId`, and `PhoneNumber`.
    /// </returns>
    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        return Ok(new
        {
            UserId = HttpContext.User.FindFirstValue(AppClaimTypes.Subject),
            Email = HttpContext.User.FindFirstValue(AppClaimTypes.Email),
            Name = HttpContext.User.FindFirstValue(AppClaimTypes.Name),
            Role = HttpContext.User.FindFirstValue(AppClaimTypes.Role),
            EmployeeId = HttpContext.User.FindFirstValue(AppClaimTypes.EmployeeId),
            PhoneNumber = HttpContext.User.FindFirstValue(AppClaimTypes.PhoneNumber)
        });
    }
}
