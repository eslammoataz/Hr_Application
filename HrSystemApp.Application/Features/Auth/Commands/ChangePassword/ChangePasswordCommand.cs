using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;

namespace HrSystemApp.Application.Features.Auth.Commands.ChangePassword;

public record ChangePasswordCommand(
    string UserId,
    string CurrentPassword,
    string NewPassword) : IRequest<Result<AuthResponse>>;
