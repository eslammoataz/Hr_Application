using Mapster;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Queries.GetEmployees;

public class GetEmployeesQueryHandler : IRequestHandler<GetEmployeesQuery, Result<PagedResult<EmployeeResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetEmployeesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PagedResult<EmployeeResponse>>> Handle(GetEmployeesQuery request, CancellationToken cancellationToken)
    {
        var paged = await _unitOfWork.Employees.GetPagedAsync(
            request.CompanyId, request.TeamId, request.SearchTerm,
            request.PageNumber, request.PageSize, cancellationToken);

        var items = paged.Items.Adapt<List<EmployeeResponse>>();
        var userIds = paged.Items
            .Select(x => x.UserId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct()
            .ToList();

        var rolesByUserId = await _unitOfWork.Users.GetPrimaryRolesByUserIdsAsync(userIds, cancellationToken);

        for (var i = 0; i < paged.Items.Count; i++)
        {
            items[i] = items[i] with
            {
                Role = paged.Items[i].UserId is not null &&
                       rolesByUserId.TryGetValue(paged.Items[i].UserId!, out var role)
                    ? role
                    : string.Empty
            };
        }

        return Result.Success(PagedResult<EmployeeResponse>.Create(
            items, paged.PageNumber, paged.PageSize, paged.TotalCount));
    }
}
