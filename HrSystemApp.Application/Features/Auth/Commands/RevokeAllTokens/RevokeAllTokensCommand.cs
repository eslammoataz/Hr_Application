using MediatR;
using HrSystemApp.Application.Common;

namespace HrSystemApp.Application.Features.Auth.Commands.RevokeAllTokens;

public record RevokeAllTokensCommand(string UserId, string? IpAddress = null) : IRequest<Result>;
