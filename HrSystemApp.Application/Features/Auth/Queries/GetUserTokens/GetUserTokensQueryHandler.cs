using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;
using HrSystemApp.Application.Interfaces;

namespace HrSystemApp.Application.Features.Auth.Queries.GetUserTokens;

public class GetUserTokensQueryHandler : IRequestHandler<GetUserTokensQuery, Result<List<RefreshTokenDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetUserTokensQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<List<RefreshTokenDto>>> Handle(GetUserTokensQuery request, CancellationToken cancellationToken)
    {
        var tokens = await _unitOfWork.RefreshTokens.GetActiveTokensByUserIdAsync(request.UserId, cancellationToken);

        var result = tokens.Select(t => new RefreshTokenDto(
            t.TokenHash,
            t.CreatedAt,
            t.ExpiresAt,
            t.IsExpired,
            t.IsRevoked,
            t.CreatedByIp
        )).ToList();

        return Result.Success(result);
    }
}
