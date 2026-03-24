using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.Auth;

public record LoginRequest(
    string Email,
    string Password,
    string? FcmToken = null,
    DeviceType? DeviceType = null,
    string? Language = null);

public record AuthResponse(
    string? Token,
    string UserId,
    string Email,
    string Name,
    string Role,
    Guid? EmployeeId,
    bool MustChangePassword,
    DateTime? ExpiresAt);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public record FirstTimeChangePasswordRequest(
    string UserId,
    string CurrentPassword,
    string NewPassword);

public record UpdateFcmTokenRequest(
    string FcmToken,
    DeviceType DeviceType);

public record UpdateLanguageRequest(
    string Language);

public record ForgotPasswordRequest(string Email, OtpChannel Channel);

public record VerifyOtpRequest(string Email, string Otp);

public record ResetPasswordRequest(string Email, string Otp, string NewPassword);
