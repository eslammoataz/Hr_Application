using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.Employees.ProfileUpdateRequests;

public class ProfileUpdateRequestDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string ChangesJson { get; set; } = string.Empty;
    public ProfileUpdateRequestStatus Status { get; set; } = ProfileUpdateRequestStatus.Pending;
    public string? EmployeeComment { get; set; }
    public string? HrNote { get; set; }
    public DateTime CreatedAt { get; set; }
}
