using MediatR;
using TestingProjectSetup.Application.Common;
using TestingProjectSetup.Application.DTOs.Auth;

namespace TestingProjectSetup.Application.Features.Auth.Commands.LoginUser;

public record LoginUserCommand(string Email, string Password) : IRequest<Result<AuthResponse>>;
