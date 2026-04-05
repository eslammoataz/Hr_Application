using MediatR;
using Microsoft.Extensions.Logging;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Features.Auth.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = _tokenService.HashToken(request.RefreshToken);
        var refreshToken = await _unitOfWork.RefreshTokens.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (refreshToken is null)
        {
            return Result.Failure<AuthResponse>(DomainErrors.Auth.InvalidRefreshToken);
        }

        // Token Reuse Detection
        if (refreshToken.RevokedAt != null)
        {
            _logger.LogWarning("Suspicious activity: Revoked refresh token reused for user {UserId}. Revoking all tokens.", refreshToken.UserId);
            await _unitOfWork.RefreshTokens.RevokeAllTokensForUserAsync(refreshToken.UserId, "Token reuse detected", request.IpAddress, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthResponse>(DomainErrors.Auth.RefreshTokenReused);
        }

        if (refreshToken.IsExpired)
        {
            return Result.Failure<AuthResponse>(DomainErrors.Auth.RefreshTokenExpired);
        }

        var user = refreshToken.User;
        if (user is null || !user.IsActive)
        {
            return Result.Failure<AuthResponse>(DomainErrors.Auth.AccountInactive);
        }

        // Logic for successful rotation
        var roles = await _unitOfWork.Users.GetRolesAsync(user);
        var (accessToken, expiresAt) = _tokenService.GenerateToken(user, roles);
        
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = _tokenService.HashToken(newRefreshToken);

        // Revoke the old token
        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.RevokedByIp = request.IpAddress;
        refreshToken.ReplacedByTokenHash = newRefreshTokenHash;
        
        // Save the new token
        await _unitOfWork.RefreshTokens.AddAsync(new HrSystemApp.Domain.Models.RefreshToken
        {
            UserId = user.Id,
            TokenHash = newRefreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_tokenService.RefreshTokenExpirationInDays),
            CreatedByIp = request.IpAddress
        }, cancellationToken);

        await _unitOfWork.RefreshTokens.UpdateAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refresh token rotated effectively for user {UserId}", user.Id);

        return Result.Success(new AuthResponse(
            Token: accessToken,
            RefreshToken: newRefreshToken,
            UserId: user.Id,
            Email: user.Email!,
            Name: user.Name,
            Role: roles.FirstOrDefault() ?? string.Empty,
            EmployeeId: user.EmployeeId,
            MustChangePassword: false,
            ExpiresAt: expiresAt,
            PhoneNumber: user.PhoneNumber,
            Language: user.Language
        ));
    }
}
