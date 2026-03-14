using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.LeaveBalances;
using MediatR;

namespace HrSystemApp.Application.Features.LeaveBalances.Queries.GetLeaveBalance;

public record GetLeaveBalanceQuery(Guid EmployeeId, int Year) : IRequest<Result<IReadOnlyList<LeaveBalanceResponse>>>;
