namespace HrSystemApp.Application.DTOs.Employees.ProfileUpdateRequests;

public class CreateProfileUpdateRequestDto
{
    public Dictionary<string, string?> NewValues { get; set; } = new();
    public string? Comment { get; set; }
}
