using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Domain.Models;

public class LeaveBalance : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public LeaveType LeaveType { get; set; }
    public int Year { get; set; }
    public decimal TotalDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal RemainingDays => TotalDays - UsedDays;

    // Navigation
    public Employee Employee { get; set; } = null!;
}
