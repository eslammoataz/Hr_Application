using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Services;

/// <summary>
/// Token service interface for JWT operations
/// </summary>
public interface ITokenService
{
    string GenerateToken(ApplicationUser user);
    Task<bool> ValidateTokenAsync(string token);
    string? GetUserIdFromToken(string token);
}
