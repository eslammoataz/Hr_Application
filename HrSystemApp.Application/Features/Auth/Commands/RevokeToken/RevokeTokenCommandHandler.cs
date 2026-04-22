using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.Auth.Commands.RevokeToken;

public class RevokeTokenCommandHandler : IRequestHandler<RevokeTokenCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RevokeTokenCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public RevokeTokenCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<RevokeTokenCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result> Handle(RevokeTokenCommand request, CancellationToken cancellationToken)
    {

        var tokenHash = _tokenService.HashToken(request.RefreshToken);
        var refreshToken = await _unitOfWork.RefreshTokens.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (refreshToken == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.RevokeToken, LogStage.Authorization,
                "TokenNotFound", new { });
            return Result.Failure(DomainErrors.Auth.InvalidRefreshToken);
        }

        if (refreshToken.RevokedAt != null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.RevokeToken, LogStage.Processing,
                "AlreadyRevoked", new { UserId = refreshToken.UserId });
            return Result.Success();
        }

        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.RevokedByIp = request.IpAddress;

        await _unitOfWork.RefreshTokens.UpdateAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}