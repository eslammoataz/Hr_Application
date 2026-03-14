using MediatR;
using HrSystemApp.Application.Common;

namespace HrSystemApp.Application.Features.Auth.Commands.ChangePassword;

public record ChangePasswordCommand(
    string UserId,
    string CurrentPassword,
    string NewPassword) : IRequest<Result>;
