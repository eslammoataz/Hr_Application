using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using Mapster;
using MediatR;

namespace HrSystemApp.Application.Features.Companies.Commands.UpdateMyCompany;

public class UpdateMyCompanyCommandHandler : IRequestHandler<UpdateMyCompanyCommand, Result<CompanyResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateMyCompanyCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<CompanyResponse>> Handle(UpdateMyCompanyCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<CompanyResponse>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
            return Result.Failure<CompanyResponse>(DomainErrors.Employee.NotFound);

        var company = await _unitOfWork.Companies.GetByIdAsync(employee.CompanyId, cancellationToken);
        if (company is null)
            return Result.Failure<CompanyResponse>(DomainErrors.General.NotFound);

        company.CompanyName = request.CompanyName;
        company.CompanyLogoUrl = request.CompanyLogoUrl;
        company.YearlyVacationDays = request.YearlyVacationDays;

        await _unitOfWork.Companies.UpdateAsync(company, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(company.Adapt<CompanyResponse>());
    }
}
