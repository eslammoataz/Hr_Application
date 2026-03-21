using Mapster;
u
ng HrSystemApp.Application.Common;
u
ng HrSystemApp.Application.DTOs.Departments;
u
ng HrSystemApp.Application.Errors;
u
ng HrSystemApp.Application.Interfaces;
u
ng MediatR;



espace HrSystemApp.Application.Features.Departments.Queries.GetDepartmentById;



lic class Ge
    DepartmentByIdQueryHandler : IRequestHandler<GetDepartmentByIdQuery, Result<DepartmentWithUnitsResponse>>
{
    private re adonly IUnitOfWork _unitOfWork;

    tByIdQueryHandler(IUnitOfWork unitOf public GetDepartmenWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DepartmentWithUnitsResponse>> Handle(GetDepartmentByIdQuery request,
        CancellationToken cancellationToken)
    {
        var department = await _unitOfWork.Departments.GetWithUnitsAsync(request.Id, cancellationToken);
        if (department is null)
            return Result.Failure<DepartmentWithUnitsResponse>(DomainErrors.Department.NotFound);

        return Result.Success(department.Adapt<DepartmentWithUnitsResponse>());
    }
}
