using Mapster;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Departments;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Departments.Queries.GetDepartmentById;

public class GetDepartmentByIdQueryHandler : IRequestHandler<GetDepartmentByIdQuery, Result<DepartmentWithUnitsResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetDepartmentByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DepartmentWithUnitsResponse>> Handle(GetDepartmentByIdQuery request, CancellationToken cancellationToken)
    {
        var department = await _unitOfWork.Departments.GetWithUnitsAsync(request.Id, cancellationToken);
        if (department is null)
            return Result.Failure<DepartmentWithUnitsResponse>(DomainErrors.Department.NotFound);

        return Result.Success(department.Adapt<DepartmentWithUnitsResponse>());
    }
}
