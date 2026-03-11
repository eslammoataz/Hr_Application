using MediatR;
using TestingProjectSetup.Application.Common;

namespace TestingProjectSetup.Application.Features.Auth.Commands.LogoutUser;

public record LogoutUserCommand(string UserId, string Token) : IRequest<Result>;
