using AutoMapper;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Queries.GetEmployees;

public class GetEmployeesQueryHandler : IRequestHandler<GetEmployeesQuery, Result<PagedResult<EmployeeResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetEmployeesQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<PagedResult<EmployeeResponse>>> Handle(GetEmployeesQuery request, CancellationToken cancellationToken)
    {
        var paged = await _unitOfWork.Employees.GetPagedAsync(
            request.CompanyId, request.TeamId, request.SearchTerm,
            request.PageNumber, request.PageSize, cancellationToken);

        var items = _mapper.Map<List<EmployeeResponse>>(paged.Items);

        return Result.Success(PagedResult<EmployeeResponse>.Create(
            items, paged.PageNumber, paged.PageSize, paged.TotalCount));
    }
}
