using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Queries.GetEmployees;

public record GetEmployeesQuery(
    Guid? CompanyId,
    string? SearchTerm,
    UserRole? Role,
    EmploymentStatus? EmploymentStatus,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<EmployeesPagedResult>>;
