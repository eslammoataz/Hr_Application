using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Features.Employees.Commands.CreateEmployee;

public class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, Result<CreateEmployeeResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmployeePlacementService _placementService;

    public CreateEmployeeCommandHandler(IUnitOfWork unitOfWork, IEmployeePlacementService placementService)
    {
        _unitOfWork = unitOfWork;
        _placementService = placementService;
    }

    public async Task<Result<CreateEmployeeResponse>> Handle(
        CreateEmployeeCommand request,
        CancellationToken cancellationToken)
    {
        var existingUsers = await _unitOfWork.Users.FindAsync(
            u => u.Email == request.Email || u.PhoneNumber == request.PhoneNumber,
            cancellationToken);

        if (existingUsers.Any())
            return Result.Failure<CreateEmployeeResponse>(DomainErrors.Employee.AlreadyExists);

        var company = await _unitOfWork.Companies.GetByIdAsync(request.CompanyId, cancellationToken);
        if (company == null)
            return Result.Failure<CreateEmployeeResponse>(DomainErrors.Company.NotFound);

        var placementResult = await _placementService.ResolvePlacementAsync(
            request.CompanyId, request.DepartmentId, request.UnitId, request.TeamId, cancellationToken);
        if (placementResult.IsFailure)
            return Result.Failure<CreateEmployeeResponse>(placementResult.Error);

        // 1. Prepare Entities
        var employeeId = Guid.NewGuid();
        var employeeCode = $"EMP-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            Name = request.FullName,
            PhoneNumber = request.PhoneNumber,
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = true,
            EmployeeId = employeeId
        };

        var employee = new Employee
        {
            Id = employeeId,
            CompanyId = request.CompanyId,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            EmployeeCode = employeeCode,
            EmploymentStatus = EmploymentStatus.Active,
            UserId = user.Id,
            DepartmentId = placementResult.Value.DepartmentId,
            UnitId = placementResult.Value.UnitId,
            TeamId = placementResult.Value.TeamId
        };

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            // 2. User Creation
            var created =
                await _unitOfWork.Users.CreateUserAsync(user, request.PhoneNumber, request.Role, cancellationToken);
            if (!created)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<CreateEmployeeResponse>(DomainErrors.Employee.CreationFailed);
            }

            // 3. Employee Creation
            await _unitOfWork.Employees.AddAsync(employee, cancellationToken);

            // 4. Leadership assignment (for leadership roles only)
            var leadershipAssignmentResult = await _placementService.AssignLeadershipIfNeededAsync(employee, request.Role, cancellationToken);
            if (leadershipAssignmentResult.IsFailure)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<CreateEmployeeResponse>(leadershipAssignmentResult.Error);
            }

            // 5. Vacation Balance
            var initialBalance = new LeaveBalance
            {
                EmployeeId = employee.Id,
                LeaveType = LeaveType.Annual,
                Year = DateTime.UtcNow.Year,
                TotalDays = company.YearlyVacationDays,
                UsedDays = 0
            };
            await _unitOfWork.LeaveBalances.AddAsync(initialBalance, cancellationToken);

            // 6. Atomic Commit
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result.Success(new CreateEmployeeResponse
            {
                EmployeeId = employee.Id,
                UserId = user.Id,
                FullName = employee.FullName,
                Email = employee.Email,
                PhoneNumber = employee.PhoneNumber,
                EmployeeCode = employee.EmployeeCode,
                Role = request.Role.ToString(),
                TemporaryPassword = request.PhoneNumber
            });
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<CreateEmployeeResponse>(DomainErrors.General.ServerError);
        }
    }
}
