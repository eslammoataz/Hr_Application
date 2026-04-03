using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Companies.Commands.ChangeCompanyStatus;

public record ChangeCompanyStatusRequest(CompanyStatus Status);
