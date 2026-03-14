using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.LeaveBalances;

public record InitializeLeaveBalanceRequest(
    Guid EmployeeId,
    LeaveType LeaveType,
    int Year,
    decimal TotalDays);

public record AdjustLeaveBalanceRequest(
    decimal NewTotalDays,
    decimal? UsedDays);

public record LeaveBalanceResponse(
    Guid Id,
    Guid EmployeeId,
    string LeaveType,
    int Year,
    decimal TotalDays,
    decimal UsedDays,
    decimal RemainingDays);
