using MediatR;
using Microsoft.Extensions.Logging;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;

namespace HrSystemApp.Application.Features.Auth.Commands.LoginUser;

public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LoginUserCommandHandler> _logger;

    public LoginUserCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<LoginUserCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        // Find user by email
        var user = await _unitOfWork.Users.GetByEmailAsync(request.Email, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Login attempt with unknown email: {Email}", request.Email);
            return Result.Failure<AuthResponse>(DomainErrors.Auth.InvalidCredentials);
        }

        // Check account is active
        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt for inactive account: {Email}", request.Email);
            return Result.Failure<AuthResponse>(DomainErrors.Auth.AccountInactive);
        }

        // Validate password
        var passwordValid = await _unitOfWork.Users.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            _logger.LogWarning("Invalid password for email: {Email}", request.Email);
            return Result.Failure<AuthResponse>(DomainErrors.Auth.InvalidCredentials);
        }

        // Resolve roles from ASP.NET Identity (via repository to keep same UserManager scope)
        var roles = await _unitOfWork.Users.GetRolesAsync(user);

        // Generate JWT
        var (token, expiresAt) = _tokenService.GenerateToken(user, roles);

        // Update last login timestamp
        user.LastLoginAt = DateTime.UtcNow;
        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return Result.Success(new AuthResponse(
            Token: token,
            UserId: user.Id,
            Email: user.Email!,
            Name: user.Name,
            Role: roles.FirstOrDefault() ?? string.Empty,
            EmployeeId: user.EmployeeId,
            MustChangePassword: user.MustChangePassword,
            ExpiresAt: expiresAt
        ));
    }
}
