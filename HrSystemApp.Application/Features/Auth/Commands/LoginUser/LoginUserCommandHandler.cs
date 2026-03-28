using MediatR;
using Microsoft.Extensions.Logging;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;

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
        var user = await _unitOfWork.Users.GetByEmailWithDetailsAsync(request.Email, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Login attempt with unknown email: {Email}", request.Email);
            return Result.Failure<AuthResponse>(DomainErrors.Auth.InvalidCredentials);
        }

        // Validate password before any further checks
        var passwordValid = await _unitOfWork.Users.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            _logger.LogWarning("Invalid password for email: {Email}", request.Email);
            return Result.Failure<AuthResponse>(DomainErrors.Auth.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt for inactive account: {Email}", request.Email);
            return Result.Failure<AuthResponse>(DomainErrors.Auth.AccountInactive);
        }


        if (user.Employee?.Company != null && user.Employee.Company.Status != CompanyStatus.Active)
        {
            _logger.LogWarning("Login attempt for user linked to inactive company: {Email}, CompanyId: {CompanyId}",
                request.Email, user.Employee.Company.Id);
            return Result.Failure<AuthResponse>(DomainErrors.Auth.CompanyInactive);
        }

        if (user.Employee != null)
        {
            var blockedStatuses = new[]
            {
                EmploymentStatus.Terminated,
                EmploymentStatus.Inactive,
                EmploymentStatus.Suspended
            };

            if (blockedStatuses.Contains(user.Employee.EmploymentStatus))
            {
                _logger.LogWarning(
                    "Login attempt for employee with blocked status {Status}: {Email}, EmployeeId: {EmployeeId}",
                    user.Employee.EmploymentStatus, request.Email, user.Employee.Id);

                return Result.Failure<AuthResponse>(DomainErrors.Auth.EmployeeBlockedStatus);
            }
        }

        // Resolve roles from ASP.NET Identity (via repository to keep same UserManager scope)
        var roles = await _unitOfWork.Users.GetRolesAsync(user);
        // If user must change password, return early without generating a full JWT
        if (user.MustChangePassword)
        {
            _logger.LogInformation("User {UserId} must change password before first login", user.Id);
            return Result.Success(new AuthResponse(
                Token: null,
                RefreshToken: null,
                UserId: user.Id,
                Email: user.Email!,
                Name: user.Name,
                Role: roles.FirstOrDefault() ?? string.Empty,
                EmployeeId: user.EmployeeId,
                MustChangePassword: true,
                ExpiresAt: null
            ));
        }

        // Generate JWT
        var (token, expiresAt) = _tokenService.GenerateToken(user, roles);
        
        // Generate Refresh Token
        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _tokenService.HashToken(refreshToken);
        
        await _unitOfWork.RefreshTokens.AddAsync(new HrSystemApp.Domain.Models.RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_tokenService.RefreshTokenExpirationInDays),
            CreatedByIp = request.IpAddress
        }, cancellationToken);

        // Update user device info and last login timestamp
        user.FcmToken = request.FcmToken ?? user.FcmToken;
        user.DeviceType = request.DeviceType ?? user.DeviceType;
        user.Language = request.Language ?? user.Language;
        user.LastLoginAt = DateTime.UtcNow;

        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return Result.Success(new AuthResponse(
            Token: token,
            RefreshToken: refreshToken,
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
