using AutoMapper;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Departments;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Departments.Commands.UpdateDepartment;

public class UpdateDepartmentCommandHandler : IRequestHandler<UpdateDepartmentCommand, Result<DepartmentResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public UpdateDepartmentCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<DepartmentResponse>> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await _unitOfWork.Departments.GetByIdAsync(request.Id, cancellationToken);
        if (department is null)
            return Result.Failure<DepartmentResponse>(DomainErrors.Department.NotFound);

        if (request.Name is not null && request.Name != department.Name)
        {
            var nameExists = await _unitOfWork.Departments.ExistsAsync(
                d => d.CompanyId == department.CompanyId && d.Name == request.Name && d.Id != request.Id,
                cancellationToken);
            if (nameExists)
                return Result.Failure<DepartmentResponse>(DomainErrors.Department.AlreadyExists);
        }

        _mapper.Map(request, department);

        await _unitOfWork.Departments.UpdateAsync(department, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(_mapper.Map<DepartmentResponse>(department));
    }
}
