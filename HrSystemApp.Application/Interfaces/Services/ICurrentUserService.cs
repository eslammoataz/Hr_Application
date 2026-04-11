namespace HrSystemApp.Application.Interfaces.Services;

/// <summary>
/// Current user accessor interface
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? PhoneNumber { get; }
    string? Role { get; }
    Guid? CompanyId { get; }
    bool IsAuthenticated { get; }
}
