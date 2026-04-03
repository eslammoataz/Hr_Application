namespace HrSystemApp.Application.Features.Companies.Commands.CreateCompanyLocation;

public record CreateCompanyLocationRequest(
    string LocationName,
    string? Address,
    double? Latitude,
    double? Longitude);
