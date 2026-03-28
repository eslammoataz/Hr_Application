using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;

namespace HrSystemApp.Application.Features.Auth.Queries.GetUserTokens;

public record GetUserTokensQuery(string UserId) : IRequest<Result<List<RefreshTokenDto>>>;
