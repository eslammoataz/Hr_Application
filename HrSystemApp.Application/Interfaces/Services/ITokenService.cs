using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Services;

/// <summary>
/// Token service interface for JWT operations
/// </summary>
public interface ITokenService
{
    (string Token, DateTime ExpiresAt) GenerateToken(ApplicationUser user, IEnumerable<string> roles);
    Task<bool> ValidateTokenAsync(string token);
    string? GetUserIdFromToken(string token);
    string GenerateRefreshToken();
    string HashToken(string token);
    int RefreshTokenExpirationInDays { get; }
}
