using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees.ProfileUpdateRequests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.ProfileUpdateRequests.Queries.GetAllProfileUpdateRequests;

public class GetAllProfileUpdateRequestsQueryHandler : IRequestHandler<GetAllProfileUpdateRequestsQuery, Result<PagedResult<ProfileUpdateRequestDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAllProfileUpdateRequestsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PagedResult<ProfileUpdateRequestDto>>> Handle(GetAllProfileUpdateRequestsQuery request, CancellationToken cancellationToken)
    {
        var hrEmployee = await _unitOfWork.Employees.GetByUserIdAsync(request.HrUserId, cancellationToken);
        if (hrEmployee is null)
        {
            return Result.Failure<PagedResult<ProfileUpdateRequestDto>>(DomainErrors.Hr.EmployeeNotFound);
        }

        var result = await _unitOfWork.ProfileUpdateRequests.GetPagedRequestsByCompanyAsync(
            hrEmployee.CompanyId, 
            request.Status,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return Result.Success(result);
    }
}
