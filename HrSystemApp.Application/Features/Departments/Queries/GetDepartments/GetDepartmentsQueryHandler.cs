using Mapster;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Departments;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Departments.Queries.GetDepartments;

public class GetDepartmentsQueryHandler : IRequestHandler<GetDepartmentsQuery, Result<IReadOnlyList<DepartmentResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetDepartmentsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<DepartmentResponse>>> Handle(GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var companyExists = await _unitOfWork.Companies.ExistsAsync(c => c.Id == request.CompanyId, cancellationToken);
        if (!companyExists)
            return Result.Failure<IReadOnlyList<DepartmentResponse>>(DomainErrors.Company.NotFound);

        var departments = await _unitOfWork.Departments.GetByCompanyAsync(request.CompanyId, cancellationToken);
        return Result.Success(departments.Adapt<IReadOnlyList<DepartmentResponse>>());
    }
}
