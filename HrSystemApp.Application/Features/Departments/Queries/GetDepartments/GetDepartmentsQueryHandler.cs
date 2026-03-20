using AutoMapper;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Departments;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Departments.Queries.GetDepartments;

public class GetDepartmentsQueryHandler : IRequestHandler<GetDepartmentsQuery, Result<IReadOnlyList<DepartmentResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetDepartmentsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<DepartmentResponse>>> Handle(GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var companyExists = await _unitOfWork.Companies.ExistsAsync(c => c.Id == request.CompanyId, cancellationToken);
        if (!companyExists)
            return Result.Failure<IReadOnlyList<DepartmentResponse>>(DomainErrors.Company.NotFound);

        var departments = await _unitOfWork.Departments.GetByCompanyAsync(request.CompanyId, cancellationToken);
        return Result.Success(_mapper.Map<IReadOnlyList<DepartmentResponse>>(departments));
    }
}
