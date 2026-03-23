using MediatR;
using Microsoft.Extensions.Logging;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;

namespace HrSystemApp.Application.Features.Auth.Commands.ForceChangePassword;

public class ForceChangePasswordCommandHandler : IRequestHandler<ForceChangePasswordCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<ForceChangePasswordCommandHandler> _logger;

    public ForceChangePasswordCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<ForceChangePasswordCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> Handle(ForceChangePasswordCommand request, CancellationToken cancellationToken)
    {
        // Find user by ID
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Force password change attempt for unknown user ID: {UserId}", request.UserId);
            return Result.Failure<AuthResponse>(DomainErrors.User.NotFound);
        }

        // Verify that the user is actually forced to change their password
        if (!user.MustChangePassword)
        {
            _logger.LogWarning("User {UserId} attempted forced password change but is not flagged for it", user.Id);
            return Result.Failure<AuthResponse>(new Error("Auth.ForcedChangeNotRequired", "User is not required to change their password via this endpoint"));
        }

        // Change password using Identity
        var (succeeded, errors) = await _unitOfWork.Users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        
        if (!succeeded)
        {
            var errorMessage = string.Join(". ", errors);
            _logger.LogWarning("Forced password change failed for user {UserId}: {Errors}", user.Id, errorMessage);
            return Result.Failure<AuthResponse>(new Error("Auth.PasswordChangeFailed", errorMessage));
        }

        // Clear the forced-change flag and update the login state
        user.MustChangePassword = false;
        user.LastLoginAt = DateTime.UtcNow;

        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Since this is effectively their first successful login, return a full AuthResponse
        var roles = await _unitOfWork.Users.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.GenerateToken(user, roles);

        _logger.LogInformation("User {UserId} successfully completed forced password change and is now logged in", user.Id);

        return Result.Success(new AuthResponse(
            Token: token,
            UserId: user.Id,
            Email: user.Email!,
            Name: user.Name,
            Role: roles.FirstOrDefault() ?? string.Empty,
            EmployeeId: user.EmployeeId,
            MustChangePassword: false,
            ExpiresAt: expiresAt
        ));
    }
}
