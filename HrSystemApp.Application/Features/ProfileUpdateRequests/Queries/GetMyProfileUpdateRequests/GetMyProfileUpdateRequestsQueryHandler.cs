using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees.ProfileUpdateRequests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.ProfileUpdateRequests.Queries.GetMyProfileUpdateRequests;

public class GetMyProfileUpdateRequestsQueryHandler : IRequestHandler<GetMyProfileUpdateRequestsQuery,
    Result<PagedResult<ProfileUpdateRequestDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMyProfileUpdateRequestsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PagedResult<ProfileUpdateRequestDto>>> Handle(GetMyProfileUpdateRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var employee = await _unitOfWork.Employees.GetByUserIdAsync(request.UserId, cancellationToken);
        if (employee is null)
            return Result.Failure<PagedResult<ProfileUpdateRequestDto>>(DomainErrors.Employee.NotFound);

        var result = await _unitOfWork.ProfileUpdateRequests.GetPagedMyRequestsAsync(
            employee.Id,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return Result.Success(result);
    }
}
