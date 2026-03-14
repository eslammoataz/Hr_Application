using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.LeaveBalances;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.LeaveBalances.Commands.InitializeLeaveBalance;

public record InitializeLeaveBalanceCommand(
    Guid EmployeeId,
    LeaveType LeaveType,
    int Year,
    decimal TotalDays) : IRequest<Result<LeaveBalanceResponse>>;
