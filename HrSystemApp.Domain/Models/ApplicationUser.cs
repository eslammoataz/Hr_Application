using HrSystemApp.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace HrSystemApp.Domain.Models;

public class ApplicationUser : IdentityUser
{
    public Guid? EmployeeId { get; set; }

    public UserRole Role { get; set; }
    public string? FcmToken { get; set; }
    public DeviceType? DeviceType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // Navigation
    public Employee? Employee { get; set; }
}
