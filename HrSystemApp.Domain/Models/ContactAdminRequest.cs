using HrSystemApp.Domain.Common;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Domain.Models;

public class ContactAdminRequest : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public ContactAdminRequestStatus Status { get; set; } = ContactAdminRequestStatus.Pending;
}
