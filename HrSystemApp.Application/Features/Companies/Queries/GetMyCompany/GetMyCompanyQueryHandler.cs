using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using Mapster;
using MediatR;

namespace HrSystemApp.Application.Features.Companies.Queries.GetMyCompany;

public class GetMyCompanyQueryHandler : IRequestHandler<GetMyCompanyQuery, Result<CompanyResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetMyCompanyQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<CompanyResponse>> Handle(GetMyCompanyQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<CompanyResponse>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
            return Result.Failure<CompanyResponse>(DomainErrors.Employee.NotFound);

        var company = await _unitOfWork.Companies.GetWithDetailsAsync(
            employee.CompanyId, 
            request.IncludeLocations, 
            request.IncludeDepartments, 
            cancellationToken);

        if (company is null)
            return Result.Failure<CompanyResponse>(DomainErrors.General.NotFound);

        return Result.Success(company.Adapt<CompanyResponse>());
    }
}
