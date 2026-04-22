using System.Globalization;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Constants;
using Microsoft.AspNetCore.Localization;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HrSystemApp.Api.Localization;

/// <summary>
/// Falls back to the authenticated user's saved language when Accept-Language is absent.
/// </summary>
public sealed class UserLanguageRequestCultureProvider : RequestCultureProvider
{
    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var authHeader = httpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var handler = new JsonWebTokenHandler();
        if (!handler.CanReadToken(token))
        {
            return null;
        }

        string? userId;
        try
        {
            var jwt = handler.ReadJsonWebToken(token);
            userId = jwt.Claims.FirstOrDefault(c => c.Type == AppClaimTypes.Subject)?.Value;
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var userRepository = httpContext.RequestServices.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByIdAsync(userId, httpContext.RequestAborted);
        var culture = NormalizeCulture(user?.Language);

        return culture is null
            ? null
            : new ProviderCultureResult(culture, culture);
    }

    private static string? NormalizeCulture(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        try
        {
            var normalized = CultureInfo.GetCultureInfo(language).TwoLetterISOLanguageName.ToLowerInvariant();
            return normalized is "en" or "ar" ? normalized : null;
        }
        catch (CultureNotFoundException)
        {
            var shortCode = language.Trim().ToLowerInvariant();
            return shortCode is "en" or "ar" ? shortCode : null;
        }
    }
}
