using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Queries.GetEmployees;

public record GetEmployeesQuery(
    Guid? CompanyId,
    Guid? TeamId,
    string? SearchTerm,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<EmployeeResponse>>>;
