using AutoMapper;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.LeaveBalances;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.LeaveBalances.Queries.GetLeaveBalance;

public class GetLeaveBalanceQueryHandler : IRequestHandler<GetLeaveBalanceQuery, Result<IReadOnlyList<LeaveBalanceResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetLeaveBalanceQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<LeaveBalanceResponse>>> Handle(GetLeaveBalanceQuery request, CancellationToken cancellationToken)
    {
        var balances = await _unitOfWork.LeaveBalances.GetByEmployeeAsync(request.EmployeeId, request.Year, cancellationToken);
        return Result.Success(_mapper.Map<IReadOnlyList<LeaveBalanceResponse>>(balances));
    }
}
