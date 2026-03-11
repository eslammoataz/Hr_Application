using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;
using MediatR;

namespace HrSystemApp.Application.Features.Auth.Commands.RegisterUser;

public record RegisterUserCommand(string Name, string Email, string PhoneNumber, string Password) : IRequest<Result<AuthResponse>>;
