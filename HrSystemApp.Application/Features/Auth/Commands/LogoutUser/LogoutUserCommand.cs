using MediatR;
using HrSystemApp.Application.Common;

namespace HrSystemApp.Application.Features.Auth.Commands.LogoutUser;

public record LogoutUserCommand(string UserId, string Token) : IRequest<Result>;
