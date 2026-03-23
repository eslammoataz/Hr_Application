using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.Auth.Commands.UpdateFcmToken;

public record UpdateFcmTokenCommand(
    string UserId,
    string FcmToken,
    DeviceType DeviceType) : IRequest<Result>;
