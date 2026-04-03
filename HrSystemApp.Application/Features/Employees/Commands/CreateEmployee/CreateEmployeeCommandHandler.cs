using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Features.Employees.Commands.CreateEmployee;

public class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, Result<CreateEmployeeResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateEmployeeCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateEmployeeResponse>> Handle(
        CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        // Check for duplicate email || duplicate phone
        var existingUser = await _unitOfWork.Users.FindAsync(
            u => u.Email == request.Email || u.PhoneNumber == request.PhoneNumber,
            cancellationToken);

        if (existingUser.Any())
            return Result.Failure<CreateEmployeeResponse>(DomainErrors.Employee.AlreadyExists);

        // Check if company exists and get its settings
        var company = await _unitOfWork.Companies.GetByIdAsync(request.CompanyId, cancellationToken);
        if (company == null)
            return Result.Failure<CreateEmployeeResponse>(DomainErrors.Company.NotFound);

        var employeeCode = $"EMP-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        // Both IDs are auto-generated in their constructors,
        // so we can cross-link them before any DB call.

        var employeeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var employee = new Employee
        {
            Id = employeeId,
            CompanyId = request.CompanyId,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            EmployeeCode = employeeCode,
            EmploymentStatus = EmploymentStatus.Active
        };

        var user = new ApplicationUser
        {
            Id = userId.ToString(),
            UserName = request.Email,
            Email = request.Email,
            Name = request.FullName,
            PhoneNumber = request.PhoneNumber,
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = true, // Temp password = phone number, must change on first login
            EmployeeId = employee.Id // employee.Id auto-set by BaseEntity
        };

        employee.UserId = user.Id; // user.Id auto-set by IdentityUser

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // 1. Create Identity user first (password = phone number)
            //    The Employee row has a FK on UserId, so the User must exist in the DB first.
            var created =
                await _unitOfWork.Users.CreateUserAsync(user, request.PhoneNumber, request.Role, cancellationToken);
            if (!created)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<CreateEmployeeResponse>(DomainErrors.Employee.CreationFailed);
            }

            // 2. Now save the employee record (UserId FK is satisfied)
            await _unitOfWork.Employees.AddAsync(employee, cancellationToken);

            // 3. Automatically initialize annual leave balance for the new employee (Full Amount)
            var initialBalance = new LeaveBalance
            {
                EmployeeId = employee.Id,
                LeaveType = LeaveType.Annual,
                Year = DateTime.UtcNow.Year,
                TotalDays = company.YearlyVacationDays,
                UsedDays = 0
            };

            await _unitOfWork.LeaveBalances.AddAsync(initialBalance, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<CreateEmployeeResponse>(DomainErrors.General.ServerError);
        }

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
}
