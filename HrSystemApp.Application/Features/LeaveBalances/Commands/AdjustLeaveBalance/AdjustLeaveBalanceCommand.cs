using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.LeaveBalances;
using MediatR;

namespace HrSystemApp.Application.Features.LeaveBalances.Commands.AdjustLeaveBalance;

public record AdjustLeaveBalanceCommand(
    Guid Id,
    decimal NewTotalDays,
    decimal? UsedDays) : IRequest<Result<LeaveBalanceResponse>>;
