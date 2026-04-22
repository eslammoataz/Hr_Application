using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Application.Features.Admin.Commands.UpdateEmployeeBalance;

public record UpdateEmployeeBalanceCommand(
    Guid EmployeeId, 
    LeaveType LeaveType, 
    int Year, 
    decimal TotalDays) : IRequest<Result<bool>>;

public class UpdateEmployeeBalanceCommandHandler : IRequestHandler<UpdateEmployeeBalanceCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UpdateEmployeeBalanceCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public UpdateEmployeeBalanceCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<UpdateEmployeeBalanceCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<bool>> Handle(UpdateEmployeeBalanceCommand request, CancellationToken cancellationToken)
    {

        var adminUserId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(adminUserId))
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateEmployeeBalance, LogStage.Authorization,
                "AdminNotAuthenticated", null);
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);
        }

        var admin = await _unitOfWork.Employees.GetByUserIdAsync(adminUserId, cancellationToken);
        if (admin == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateEmployeeBalance, LogStage.Authorization,
                "AdminNotFound", new { AdminUserId = adminUserId });
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);
        }

        var targetEmployee = await _unitOfWork.Employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (targetEmployee == null || targetEmployee.CompanyId != admin.CompanyId)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateEmployeeBalance, LogStage.Authorization,
                "UnauthorizedCrossCompanyAccess", new { AdminId = admin.Id, EmployeeId = request.EmployeeId });
            return Result.Failure<bool>(DomainErrors.Requests.Unauthorized);
        }

        var balance = await _unitOfWork.LeaveBalances.GetAsync(request.EmployeeId, request.LeaveType, request.Year, cancellationToken);

        if (balance == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateEmployeeBalance, LogStage.Processing,
                "CreatingNewBalance", new { EmployeeId = request.EmployeeId, LeaveType = request.LeaveType.ToString(), Year = request.Year });

            balance = new Domain.Models.LeaveBalance
            {
                EmployeeId = request.EmployeeId,
                LeaveType = request.LeaveType,
                Year = request.Year,
                TotalDays = request.TotalDays,
                UsedDays = 0
            };
            await _unitOfWork.LeaveBalances.AddAsync(balance, cancellationToken);
        }
        else
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.UpdateEmployeeBalance, LogStage.Processing,
                "UpdatingExistingBalance", new { EmployeeId = request.EmployeeId, LeaveType = request.LeaveType.ToString(), Year = request.Year, NewTotal = request.TotalDays });

            balance.TotalDays = request.TotalDays;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
