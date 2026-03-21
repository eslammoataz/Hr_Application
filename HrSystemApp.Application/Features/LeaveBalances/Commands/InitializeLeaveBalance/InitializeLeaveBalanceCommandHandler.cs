using Mapster;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.LeaveBalances;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.LeaveBalances.Commands.InitializeLeaveBalance;

public class InitializeLeaveBalanceCommandHandler : IRequestHandler<InitializeLeaveBalanceCommand, Result<LeaveBalanceResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public InitializeLeaveBalanceCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LeaveBalanceResponse>> Handle(InitializeLeaveBalanceCommand request, CancellationToken cancellationToken)
    {
        var employeeExists = await _unitOfWork.Employees.ExistsAsync(e => e.Id == request.EmployeeId, cancellationToken);
        if (!employeeExists)
            return Result.Failure<LeaveBalanceResponse>(DomainErrors.Employee.NotFound);

        var existing = await _unitOfWork.LeaveBalances.GetAsync(request.EmployeeId, request.LeaveType, request.Year, cancellationToken);
        if (existing is not null)
            return Result.Failure<LeaveBalanceResponse>(DomainErrors.LeaveBalance.AlreadyInitialized);

        var balance = new LeaveBalance
        {
            EmployeeId = request.EmployeeId,
            LeaveType  = request.LeaveType,
            Year       = request.Year,
            TotalDays  = request.TotalDays,
            UsedDays   = 0
        };

        await _unitOfWork.LeaveBalances.AddAsync(balance, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(balance.Adapt<LeaveBalanceResponse>());
    }
}
