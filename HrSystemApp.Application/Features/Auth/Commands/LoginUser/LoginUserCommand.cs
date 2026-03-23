using HrSystemApp.Domain.Enums;
using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;

namespace HrSystemApp.Application.Features.Auth.Commands.LoginUser;

public record LoginUserCommand(
    string Email,
    string Password,
    string? FcmToken = null,
    DeviceType? DeviceType = null,
    string? Language = null) : IRequest<Result<AuthResponse>>;
