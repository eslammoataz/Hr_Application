namespace HrSystemApp.Application.DTOs.Companies;

public record CompanyLocationResponse(
    Guid Id,
    Guid CompanyId,
    string LocationName,
    string? Address,
    double? Latitude,
    double? Longitude);
