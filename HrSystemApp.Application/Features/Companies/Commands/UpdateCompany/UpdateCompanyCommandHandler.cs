using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using Mapster;
using MediatR;

namespace HrSystemApp.Application.Features.Companies.Commands.UpdateCompany;

public class UpdateCompanyCommandHandler : IRequestHandler<UpdateCompanyCommand, Result<CompanyResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateCompanyCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<CompanyResponse>> Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await _unitOfWork.Companies.GetByIdAsync(request.Id, cancellationToken);
        if (company is null)
            return Result.Failure<CompanyResponse>(DomainErrors.General.NotFound);

        if (_currentUserService.Role != nameof(UserRole.SuperAdmin))
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Result.Failure<CompanyResponse>(DomainErrors.Auth.Unauthorized);

            var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
            if (employee == null)
                return Result.Failure<CompanyResponse>(DomainErrors.Employee.NotFound);

            if (request.Id != employee.CompanyId)
                return Result.Failure<CompanyResponse>(DomainErrors.General.Forbidden);
        }

        company.CompanyName = request.CompanyName;
        company.CompanyLogoUrl = request.CompanyLogoUrl;
        company.YearlyVacationDays = request.YearlyVacationDays;
        company.Status = request.Status;

        await _unitOfWork.Companies.UpdateAsync(company, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(company.Adapt<CompanyResponse>());
    }
}
