using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Constants;
using System.Text.Json;

namespace HrSystemApp.Api;

public static class DependencyInjection
{
    /// <summary>
    /// Registers API framework services and configures JWT Bearer authentication using the
    /// "JwtSettings" configuration section.
    /// </summary>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the JWT secret is missing ("JWT Secret not configured") or shorter than 32 characters ("JWT Secret must be at least 32 characters.").</exception>
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        services.AddControllers();

        var jwtSettings = new HrSystemApp.Application.Settings.JwtSettings();
        configuration.GetSection("JwtSettings").Bind(jwtSettings);

        if (string.IsNullOrEmpty(jwtSettings.Secret))
            throw new InvalidOperationException("JWT Secret not configured");

        if (jwtSettings.Secret.Length < 32)
            throw new InvalidOperationException("JWT Secret must be at least 32 characters.");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.SaveToken = true;
            options.RequireHttpsMetadata = false;
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,

                NameClaimType = "name",
                RoleClaimType = AppClaimTypes.Role
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    context.HttpContext.RequestServices.GetService<ILoggerFactory>()
                        ?.CreateLogger("JwtBearer")
                        ?.LogWarning(context.Exception, "JWT authentication failed");
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";

                    var error = string.IsNullOrEmpty(context.Error)
                        ? DomainErrors.Auth.Unauthorized
                        : new Error("Auth.InvalidToken", context.ErrorDescription ?? context.Error);

                    var result = Result.Failure(error);
                    var body = JsonSerializer.Serialize(new
                    {
                        result.IsSuccess,
                        result.IsFailure,
                        Error = new { result.Error.Code, result.Error.Message }
                    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                    return context.Response.WriteAsync(body);
                }
            };
        });

        return services;
    }
}