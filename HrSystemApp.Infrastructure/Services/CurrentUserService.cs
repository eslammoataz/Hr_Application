using HrSystemApp.Application.Interfaces.Services;
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

    // MapInboundClaims = false in JWT config → claims stay as raw names ("sub", "role")
    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue("sub");

    public string? PhoneNumber => _httpContextAccessor.HttpContext?.User?.FindFirstValue("phone");

    public string? Role => _httpContextAccessor.HttpContext?.User?.FindFirstValue("role");

    public Guid? CompanyId
    {
        get
        {
            var rawValue = _httpContextAccessor.HttpContext?.User?.FindFirstValue("companyId");
            return Guid.TryParse(rawValue, out var companyId) ? companyId : null;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}

