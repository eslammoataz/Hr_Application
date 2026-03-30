using HrSystemApp.Application.Common;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.Requests.Queries.GetMyLeaveBalances;

public record GetMyLeaveBalancesQuery : IRequest<Result<List<LeaveBalanceDto>>>;

public record LeaveBalanceDto
{
    public LeaveType LeaveType { get; set; }
    public int Year { get; set; }
    public decimal TotalDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal RemainingDays { get; set; }
}

public class GetMyLeaveBalancesQueryHandler : IRequestHandler<GetMyLeaveBalancesQuery, Result<List<LeaveBalanceDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetMyLeaveBalancesQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<LeaveBalanceDto>>> Handle(GetMyLeaveBalancesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<List<LeaveBalanceDto>>(new Error("Auth.Unauthorized", "User not authenticated."));

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
            return Result.Failure<List<LeaveBalanceDto>>(new Error("Employee.NotFound", "Employee profile not found."));

        var currentYear = DateTime.UtcNow.Year;
        var balances = await _unitOfWork.LeaveBalances.GetByEmployeeAsync(employee.Id, currentYear, cancellationToken);

        var dtos = balances.Select(b => new LeaveBalanceDto
        {
            LeaveType = b.LeaveType,
            Year = b.Year,
            TotalDays = b.TotalDays,
            UsedDays = b.UsedDays,
            RemainingDays = b.RemainingDays
        }).ToList();

        return Result.Success(dtos);
    }
}
