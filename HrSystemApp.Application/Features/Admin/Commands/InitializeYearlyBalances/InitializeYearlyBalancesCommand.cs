using HrSystemApp.Application.Common;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.Admin.Commands.InitializeYearlyBalances;

public record InitializeYearlyBalancesCommand(int Year) : IRequest<Result<int>>;

public class InitializeYearlyBalancesCommandHandler : IRequestHandler<InitializeYearlyBalancesCommand, Result<int>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<InitializeYearlyBalancesCommandHandler> _logger;

    public InitializeYearlyBalancesCommandHandler(
        IUnitOfWork unitOfWork, 
        ICurrentUserService currentUserService, 
        ILogger<InitializeYearlyBalancesCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<int>> Handle(InitializeYearlyBalancesCommand request, CancellationToken cancellationToken)
    {
        var adminUserId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(adminUserId))
            return Result.Failure<int>(new Error("Auth.Unauthorized", "User not authenticated."));

        var admin = await _unitOfWork.Employees.GetByUserIdAsync(adminUserId, cancellationToken);
        if (admin == null)
            return Result.Failure<int>(new Error("Employee.NotFound", "Admin profile not found."));

        var company = await _unitOfWork.Companies.GetByIdAsync(admin.CompanyId, cancellationToken);
        if (company == null)
            return Result.Failure<int>(new Error("Company.NotFound", "Company context missing."));

        // 1. Get all active employees in this company
        var employees = await _unitOfWork.Employees.FindAsync(e => 
            e.CompanyId == company.Id && 
            e.EmploymentStatus == EmploymentStatus.Active, 
            cancellationToken);

        int count = 0;
        foreach (var emp in employees)
        {
            // 2. Check if balance already exists
            var existing = await _unitOfWork.LeaveBalances.GetAsync(emp.Id, LeaveType.Annual, request.Year, cancellationToken);
            if (existing == null)
            {
                var newBalance = new LeaveBalance
                {
                    EmployeeId = emp.Id,
                    LeaveType = LeaveType.Annual,
                    Year = request.Year,
                    TotalDays = company.YearlyVacationDays,
                    UsedDays = 0
                };
                await _unitOfWork.LeaveBalances.AddAsync(newBalance, cancellationToken);
                count++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Initialized {Count} yearly balances for Company {CompanyId} for Year {Year}", 
            count, company.Id, request.Year);

        return Result.Success(count);
    }
}
