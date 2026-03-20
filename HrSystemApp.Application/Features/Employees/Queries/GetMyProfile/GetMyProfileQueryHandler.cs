using AutoMapper;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Queries.GetMyProfile;

public class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, Result<EmployeeResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetMyProfileQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<EmployeeResponse>> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var employee = await _unitOfWork.Employees.GetByUserIdAsync(request.UserId, cancellationToken);
        if (employee is null)
            return Result.Failure<EmployeeResponse>(DomainErrors.Employee.NotFound);

        var detailed = await _unitOfWork.Employees.GetWithDetailsAsync(employee.Id, cancellationToken);
        return Result.Success(_mapper.Map<EmployeeResponse>(detailed!));
    }
}
