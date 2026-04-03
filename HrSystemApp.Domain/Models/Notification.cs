using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Domain.Models;

public class Notification : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.General;
    public bool IsRead { get; set; }

    public Employee Employee { get; set; } = null!;
}
