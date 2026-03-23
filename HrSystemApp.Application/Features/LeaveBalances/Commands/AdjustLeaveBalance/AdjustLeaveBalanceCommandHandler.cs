using Mapster;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.LeaveBalances;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.LeaveBalances.Commands.AdjustLeaveBalance;

public class AdjustLeaveBalanceCommandHandler : IRequestHandler<AdjustLeaveBalanceCommand, Result<LeaveBalanceResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public AdjustLeaveBalanceCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LeaveBalanceResponse>> Handle(AdjustLeaveBalanceCommand request, CancellationToken cancellationToken)
    {
        var balance = await _unitOfWork.LeaveBalances.GetByIdAsync(request.Id, cancellationToken);
        if (balance is null)
            return Result.Failure<LeaveBalanceResponse>(DomainErrors.LeaveBalance.NotFound);

        balance.TotalDays = request.NewTotalDays;
        balance.UsedDays  = request.UsedDays ?? balance.UsedDays;

        await _unitOfWork.LeaveBalances.UpdateAsync(balance, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(balance.Adapt<LeaveBalanceResponse>());
    }
}
