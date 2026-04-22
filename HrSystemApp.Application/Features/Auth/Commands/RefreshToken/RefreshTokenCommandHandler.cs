using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.Auth.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public RefreshTokenCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<RefreshTokenCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {

        var tokenHash = _tokenService.HashToken(request.RefreshToken);
        var refreshToken = await _unitOfWork.RefreshTokens.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (refreshToken is null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.RefreshToken, LogStage.Authorization,
                "TokenNotFound", new { });
            return Result.Failure<AuthResponse>(DomainErrors.Auth.InvalidRefreshToken);
        }

        if (refreshToken.RevokedAt != null)
        {
            _logger.LogWarningUnauthorized(_loggingOptions, LogAction.Auth.RefreshToken);
            await _unitOfWork.RefreshTokens.RevokeAllTokensForUserAsync(refreshToken.UserId, "Token reuse detected", request.IpAddress, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthResponse>(DomainErrors.Auth.RefreshTokenReused);
        }

        if (refreshToken.IsExpired)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.RefreshToken, LogStage.Authorization,
                "TokenExpired", new { UserId = refreshToken.UserId });
            return Result.Failure<AuthResponse>(DomainErrors.Auth.RefreshTokenExpired);
        }

        var user = refreshToken.User;
        if (user is null || !user.IsActive)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.RefreshToken, LogStage.Authorization,
                "UserInactive", new { UserId = user?.Id });
            return Result.Failure<AuthResponse>(DomainErrors.Auth.AccountInactive);
        }

        var roles = await _unitOfWork.Users.GetRolesAsync(user);
        var (accessToken, expiresAt) = _tokenService.GenerateToken(user, roles);

        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = _tokenService.HashToken(newRefreshToken);

        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.RevokedByIp = request.IpAddress;
        refreshToken.ReplacedByTokenHash = newRefreshTokenHash;

        await _unitOfWork.RefreshTokens.AddAsync(new HrSystemApp.Domain.Models.RefreshToken
        {
            UserId = user.Id,
            TokenHash = newRefreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_tokenService.RefreshTokenExpirationInDays),
            CreatedByIp = request.IpAddress
        }, cancellationToken);

        await _unitOfWork.RefreshTokens.UpdateAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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