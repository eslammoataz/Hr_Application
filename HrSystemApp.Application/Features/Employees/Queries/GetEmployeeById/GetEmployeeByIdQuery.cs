using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Queries.GetEmployeeById;

public record GetEmployeeByIdQuery(Guid Id) : IRequest<Result<EmployeeResponse>>;
