using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(ApplicationUser user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new Dictionary<string, object>
        {
            ["sub"] = user.Id,
            ["jti"] = Guid.NewGuid().ToString(),
            ["email"] = user.Email ?? "",
            ["name"] = user.Name ?? user.UserName ?? user.PhoneNumber ?? "",
            ["phone"] = user.PhoneNumber ?? ""
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Claims = claims,
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            Expires = DateTime.UtcNow.AddMinutes(int.Parse(jwtSettings["ExpirationInMinutes"] ?? "60")),
            SigningCredentials = credentials
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(tokenDescriptor);
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

    public string? GetUserIdFromToken(string token)
    {
        var handler = new JsonWebTokenHandler();

        try
        {
            var jwtToken = handler.ReadJsonWebToken(token);
            return jwtToken.Claims
                .FirstOrDefault(c => c.Type == "sub")?.Value;
        }
        catch
        {
            return null;
        }
    }
}