using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.Companies;

public record CompanyPositionResponse(
    UserRole Role,
    string PositionTitle,
    int SortOrder);
