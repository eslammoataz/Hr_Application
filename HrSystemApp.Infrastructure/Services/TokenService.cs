using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using HrSystemApp.Domain.Constants;

namespace HrSystemApp.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Creates a signed JWT for the specified user containing their identity and primary role.
    /// </summary>
    /// <param name="user">The user for whom the token will be issued.</param>
    /// <param name="roles">The user's roles; the first role (if any) is used as the token's primary role claim.</param>
    /// <returns>A tuple where `Token` is the serialized JWT string and `ExpiresAt` is the token's UTC expiration time.</returns>
    public (string Token, DateTime ExpiresAt) GenerateToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expirationMinutes = int.Parse(jwtSettings["ExpirationInMinutes"] ?? "60");
        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var claims = new Dictionary<string, object>
        {
            ["jti"] = Guid.NewGuid().ToString(),
            [AppClaimTypes.Subject] = user.Id,
            [AppClaimTypes.Email] = user.Email ?? string.Empty,
            [AppClaimTypes.Name] = user.Name,
            [AppClaimTypes.Role] = roles.FirstOrDefault() ?? string.Empty,
            [AppClaimTypes.PhoneNumber] = user.PhoneNumber ?? string.Empty,
            [AppClaimTypes.CompanyId] = user.Employee?.CompanyId.ToString() ?? "",
        };

        if (user.EmployeeId.HasValue)
        {
            claims[AppClaimTypes.EmployeeId] = user.EmployeeId.Value.ToString();
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Claims = claims,
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            Expires = expiresAt,
            SigningCredentials = credentials
        };

        var handler = new JsonWebTokenHandler();
        return (handler.CreateToken(tokenDescriptor), expiresAt);
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!));
        var handler = new JsonWebTokenHandler();

        var result = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        });

        return result.IsValid;
    }

    /// <summary>
    /// Extracts the user identifier from a JWT's subject claim.
    /// </summary>
    /// <param name="token">The JWT string to read.</param>
    /// <returns>The subject claim value representing the user id, or <c>null</c> if the token is invalid or the subject claim is missing.</returns>
    public string? GetUserIdFromToken(string token)
    {
        var handler = new JsonWebTokenHandler();
        try
        {
            var jwtToken = handler.ReadJsonWebToken(token);
            return jwtToken.Claims.FirstOrDefault(c => c.Type == AppClaimTypes.Subject)?.Value;
        }
        catch
        {
            return null;
        }
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
    }

    public int RefreshTokenExpirationInDays =>
        int.TryParse(_configuration["JwtSettings:RefreshTokenExpirationInDays"], out int days) ? days : 30;
}