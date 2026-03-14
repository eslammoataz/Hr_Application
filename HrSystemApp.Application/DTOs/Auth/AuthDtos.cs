namespace HrSystemApp.Application.DTOs.Auth;

public record LoginRequest(string Email, string Password);

public record AuthResponse(
    string Token,
    string UserId,
    string Email,
    string Name,
    string Role,
    Guid? EmployeeId,
    bool MustChangePassword,
    DateTime ExpiresAt);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);
