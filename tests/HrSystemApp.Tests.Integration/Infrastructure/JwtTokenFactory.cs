using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using HrSystemApp.Domain.Constants;

namespace HrSystemApp.Tests.Integration.Infrastructure;

public static class JwtTokenFactory
{
    public static string CreateToken(IConfiguration configuration, string userId, string role, Guid? companyId = null)
    {
        var secret = configuration["JwtSettings:Secret"] ?? throw new InvalidOperationException("JwtSettings:Secret is missing.");
        var issuer = configuration["JwtSettings:Issuer"] ?? throw new InvalidOperationException("JwtSettings:Issuer is missing.");
        var audience = configuration["JwtSettings:Audience"] ?? throw new InvalidOperationException("JwtSettings:Audience is missing.");

        var claims = new List<Claim>
        {
            new(AppClaimTypes.Subject, userId),
            new(AppClaimTypes.Role, role),
            new(AppClaimTypes.Name, $"Test User {userId}")
        };

        if (companyId.HasValue)
        {
            claims.Add(new Claim(AppClaimTypes.CompanyId, companyId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
