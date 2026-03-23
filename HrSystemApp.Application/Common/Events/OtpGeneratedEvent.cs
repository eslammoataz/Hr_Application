using MediatR;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Common.Events;

public record OtpGeneratedEvent(
    string Email,
    string? PhoneNumber,
    string Otp,
    OtpChannel Channel) : INotification;
