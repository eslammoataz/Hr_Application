using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.Auth.Commands.RevokeAllTokens;

public class RevokeAllTokensCommandHandler : IRequestHandler<RevokeAllTokensCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RevokeAllTokensCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public RevokeAllTokensCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<RevokeAllTokensCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result> Handle(RevokeAllTokensCommand request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.Auth.RevokeAllTokens);

        await _unitOfWork.RefreshTokens.RevokeAllTokensForUserAsync(
            request.UserId, "Manual revocation", request.IpAddress, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.Auth.RevokeAllTokens, sw.ElapsedMilliseconds);

        return Result.Success();
    }
}