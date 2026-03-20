using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Departments;
using MediatR;

namespace HrSystemApp.Application.Features.Departments.Queries.GetDepartments;

public record GetDepartmentsQuery(Guid CompanyId) : IRequest<Result<IReadOnlyList<DepartmentResponse>>>;
