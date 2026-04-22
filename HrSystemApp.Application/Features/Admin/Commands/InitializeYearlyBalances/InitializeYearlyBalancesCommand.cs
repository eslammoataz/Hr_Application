using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Application.Features.Admin.Commands.InitializeYearlyBalances;

public record InitializeYearlyBalancesCommand(int Year) : IRequest<Result<int>>;

public class InitializeYearlyBalancesCommandHandler : IRequestHandler<InitializeYearlyBalancesCommand, Result<int>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<InitializeYearlyBalancesCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public InitializeYearlyBalancesCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<InitializeYearlyBalancesCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<int>> Handle(InitializeYearlyBalancesCommand request, CancellationToken cancellationToken)
    {

        var adminUserId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(adminUserId))
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.InitializeYearlyBalances, LogStage.Authorization,
                "AdminNotAuthenticated", null);
            return Result.Failure<int>(DomainErrors.Auth.Unauthorized);
        }

        var admin = await _unitOfWork.Employees.GetByUserIdAsync(adminUserId, cancellationToken);
        if (admin == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.InitializeYearlyBalances, LogStage.Authorization,
                "AdminNotFound", new { AdminUserId = adminUserId });
            return Result.Failure<int>(DomainErrors.Employee.NotFound);
        }

        var company = await _unitOfWork.Companies.GetByIdAsync(admin.CompanyId, cancellationToken);
        if (company == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.InitializeYearlyBalances, LogStage.Validation,
                "CompanyNotFound", new { CompanyId = admin.CompanyId });
            return Result.Failure<int>(DomainErrors.Company.NotFound);
        }

        var employees = await _unitOfWork.Employees.FindAsync(e =>
            e.CompanyId == company.Id &&
            e.EmploymentStatus == EmploymentStatus.Active,
            cancellationToken);

        int count = 0;
        foreach (var emp in employees)
        {
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

        return Result.Success(count);
    }
}
