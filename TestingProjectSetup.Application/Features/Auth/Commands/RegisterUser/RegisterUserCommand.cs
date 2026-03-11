using TestingProjectSetup.Application.Common;
using TestingProjectSetup.Application.DTOs.Auth;
using MediatR;

namespace TestingProjectSetup.Application.Features.Auth.Commands.RegisterUser;

public record RegisterUserCommand(string Name, string Email, string PhoneNumber, string Password) : IRequest<Result<AuthResponse>>;
