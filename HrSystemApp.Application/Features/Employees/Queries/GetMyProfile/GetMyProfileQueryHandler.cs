using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Queries.GetMyProfile;

public class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, Result<EmployeeProfileDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMyProfileQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeProfileDto>> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await _unitOfWork.Employees.GetProfileByUserIdAsync(request.UserId, cancellationToken);
        
        if (profile is null)
            return Result.Failure<EmployeeProfileDto>(DomainErrors.Employee.NotFound);

        return Result.Success(profile);
    }
}
