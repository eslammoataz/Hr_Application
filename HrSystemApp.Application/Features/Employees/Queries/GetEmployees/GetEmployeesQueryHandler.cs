using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Queries.GetEmployees;

public class GetEmployeesQueryHandler : IRequestHandler<GetEmployeesQuery, Result<EmployeesPagedResult>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDataScopeService _dataScopeService;

    public GetEmployeesQueryHandler(IUnitOfWork unitOfWork, IDataScopeService dataScopeService)
    {
        _unitOfWork = unitOfWork;
        _dataScopeService = dataScopeService;
    }

    public async Task<Result<EmployeesPagedResult>> Handle(GetEmployeesQuery request, CancellationToken cancellationToken)
    {
        var companyScopeResult = _dataScopeService.ResolveEmployeeCompanyScope(request.CompanyId);
        if (companyScopeResult.IsFailure)
        {
            return Result.Failure<EmployeesPagedResult>(companyScopeResult.Error);
        }

        var paged = await _unitOfWork.Employees.GetPagedForListAsync(
            companyScopeResult.Value,
            request.TeamId,
            request.SearchTerm,
            request.Role,
            request.EmploymentStatus,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return Result.Success(paged);
    }
}
