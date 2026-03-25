using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.ContactAdmin;

public record ContactAdminRequestDto(
    Guid Id,
    string Name,
    string Email,
    string CompanyName,
    string Role,
    string PhoneNumber,
    string Status,
    DateTime CreatedAt);
