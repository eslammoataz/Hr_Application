using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Departments;
using MediatR;

namespace HrSystemApp.Application.Features.Departments.Queries.GetDepartmentById;

public record GetDepartmentByIdQuery(Guid Id) : IRequest<Result<DepartmentWithUnitsResponse>>;
