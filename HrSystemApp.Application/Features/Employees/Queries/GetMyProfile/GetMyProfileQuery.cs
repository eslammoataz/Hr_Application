using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Queries.GetMyProfile;

public record GetMyProfileQuery(string UserId) : IRequest<Result<EmployeeResponse>>;
