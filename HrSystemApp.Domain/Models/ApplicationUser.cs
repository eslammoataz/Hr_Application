using HrSystemApp.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace HrSystemApp.Domain.Models;

public class ApplicationUser : IdentityUser
{
    public string Name { get; set; } = string.Empty;
    public Guid? EmployeeId { get; set; }

    public string? FcmToken { get; set; }
    public DeviceType? DeviceType { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public string? Language { get; set; }
    public int OtpAttempts { get; set; } = 0;

    // Navigation
    public Employee? Employee { get; set; }
}
