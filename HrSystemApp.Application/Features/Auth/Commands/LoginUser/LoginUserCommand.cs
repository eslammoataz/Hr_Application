using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;

namespace HrSystemApp.Application.Features.Auth.Commands.LoginUser;

public record LoginUserCommand(string Email, string Password) : IRequest<Result<AuthResponse>>;
