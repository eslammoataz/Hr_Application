using MediatR;
using Microsoft.Extensions.Logging;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;

namespace HrSystemApp.Application.Features.Auth.Commands.ChangePassword;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        // Find user by ID
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Password change attempt for unknown user ID: {UserId}", request.UserId);
            return Result.Failure<AuthResponse>(DomainErrors.User.NotFound);
        }

        // Change password using Identity
        var (succeeded, errors) =
            await _unitOfWork.Users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!succeeded)
        {
            var errorMessage = string.Join(". ", errors);
            _logger.LogWarning("Password change failed for user {UserId}: {Errors}", user.Id, errorMessage);
            return Result.Failure<AuthResponse>(new Error("Auth.PasswordChangeFailed", errorMessage));
        }

        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Optional: In regular change, we might not even need to return a new token, 
        // but keeping it for consistency if the frontend expects it.
        var roles = await _unitOfWork.Users.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.GenerateToken(user, roles);

        _logger.LogInformation("User {UserId} successfully changed their password", user.Id);

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
