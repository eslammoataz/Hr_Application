namespace HrSystemApp.Application.DTOs.Employees.ProfileUpdateRequests;

public class ProfileUpdateRequestDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string ChangesJson { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? EmployeeComment { get; set; }
    public string? HrNote { get; set; }
    public DateTime CreatedAt { get; set; }
}
