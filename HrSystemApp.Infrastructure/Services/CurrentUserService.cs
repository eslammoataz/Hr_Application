using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Constants;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace HrSystemApp.Infrastructure.Services;

/// <summary>
/// Current user service implementation
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // MapInboundClaims = false in JWT config → claims stay as raw names
    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(AppClaimTypes.Subject);

    public string? PhoneNumber => _httpContextAccessor.HttpContext?.User?.FindFirstValue(AppClaimTypes.PhoneNumber);

    public string? Role => _httpContextAccessor.HttpContext?.User?.FindFirstValue(AppClaimTypes.Role);

    public Guid? CompanyId
    {
        get
        {
            var rawValue = _httpContextAccessor.HttpContext?.User?.FindFirstValue(AppClaimTypes.CompanyId);
            return Guid.TryParse(rawValue, out var companyId) ? companyId : null;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}

