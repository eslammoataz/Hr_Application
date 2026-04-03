namespace HrSystemApp.Application.DTOs.Employees;

public record CreateEmployeeResponse
{
    public Guid EmployeeId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string EmployeeCode { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string TemporaryPassword { get; init; } = string.Empty;
}
