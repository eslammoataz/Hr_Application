using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Auth.Commands.ForgotPassword;

public record ForgotPasswordCommand(string Email, OtpChannel Channel) : IRequest<Result>;
