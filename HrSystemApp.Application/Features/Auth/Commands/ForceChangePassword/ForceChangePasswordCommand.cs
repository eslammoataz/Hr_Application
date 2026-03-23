using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;

namespace HrSystemApp.Application.Features.Auth.Commands.ForceChangePassword;

public record ForceChangePasswordCommand(
    string UserId,
    string CurrentPassword,
    string NewPassword) : IRequest<Result<AuthResponse>>;
