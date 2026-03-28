using MediatR;
using Microsoft.Extensions.Logging;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Interfaces;

namespace HrSystemApp.Application.Features.Auth.Commands.RevokeAllTokens;

public class RevokeAllTokensCommandHandler : IRequestHandler<RevokeAllTokensCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RevokeAllTokensCommandHandler> _logger;

    public RevokeAllTokensCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<RevokeAllTokensCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(RevokeAllTokensCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.RefreshTokens.RevokeAllTokensForUserAsync(request.UserId, "Manual revocation", request.IpAddress, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("All tokens revoked for user {UserId}", request.UserId);

        return Result.Success();
    }
}
