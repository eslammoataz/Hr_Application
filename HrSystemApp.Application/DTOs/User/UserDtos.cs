namespace HrSystemApp.Application.DTOs.User;

public record UserDto(
    string Id,
    string PhoneNumber,
    string Name,
    string? Email,
    string? Country,
    string? City,
    string? State,
    bool IsActive,
    DateTime CreatedAt);

public record UpdateUserRequest(
    string? Name,
    string? Email,
    string? Country,
    string? City,
    string? State);

public record CreateUserRequest(
    string PhoneNumber,
    string Name,
    string? Email = null,
    string? Country = null,
    string? City = null,
    string? State = null);
