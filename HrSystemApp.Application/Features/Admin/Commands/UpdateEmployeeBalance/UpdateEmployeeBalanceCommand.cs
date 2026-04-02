using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

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

    public UpdateEmployeeBalanceCommandHandler(
        IUnitOfWork unitOfWork, 
        ICurrentUserService currentUserService, 
        ILogger<UpdateEmployeeBalanceCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(UpdateEmployeeBalanceCommand request, CancellationToken cancellationToken)
    {
        var adminUserId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(adminUserId))
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);

        var admin = await _unitOfWork.Employees.GetByUserIdAsync(adminUserId, cancellationToken);
        if (admin == null)
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);

        // 1. Security check: Only CompanyAdmin or HR
        // Note: For simplicity here we check CompanyId match. Role check is handled at Controller layer via [Authorize]
        var targetEmployee = await _unitOfWork.Employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (targetEmployee == null || targetEmployee.CompanyId != admin.CompanyId)
        {
            _logger.LogWarning("Admin {AdminId} attempted to update balance for employee {EmployeeId} in a different company.", 
                admin.Id, request.EmployeeId);
            return Result.Failure<bool>(DomainErrors.Requests.Unauthorized);
        }

        // 2. Fetch/Update Balance
        var balance = await _unitOfWork.LeaveBalances.GetAsync(request.EmployeeId, request.LeaveType, request.Year, cancellationToken);
        
        if (balance == null)
        {
            // Initializing new balance record if it doesn't exist
            _logger.LogInformation("Creating new leave balance for Employee {EmployeeId}, Type {Type}, Year {Year}", 
                request.EmployeeId, request.LeaveType, request.Year);
                
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
            _logger.LogInformation("Updating leave balance for Employee {EmployeeId}, Type {Type}, Year {Year}. New Total: {Total}", 
                request.EmployeeId, request.LeaveType, request.Year, request.TotalDays);
                
            balance.TotalDays = request.TotalDays;
            // Note: We don't change UsedDays unless explicitly asked in a separate command, 
            // as it should be driven by approved requests.
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
