namespace HrSystemApp.Application.DTOs.Auth;

public record RegisterRequest(string Name, string Email, string PhoneNumber, string Password);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, string UserId, string Email, string Name, DateTime ExpiresAt);

public record LogoutRequest(string Token);
