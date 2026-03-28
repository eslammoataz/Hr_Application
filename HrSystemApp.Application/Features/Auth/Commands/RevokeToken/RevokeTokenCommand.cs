using MediatR;
using HrSystemApp.Application.Common;

namespace HrSystemApp.Application.Features.Auth.Commands.RevokeToken;

public record RevokeTokenCommand(string RefreshToken, string? IpAddress = null) : IRequest<Result>;
