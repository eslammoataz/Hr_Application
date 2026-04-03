using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HrSystemApp.Application.Features.Employees.Commands.CreateEmployee;

public class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, Result<CreateEmployeeResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateEmployeeCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var created = await _unitOfWork.Users.CreateUserAsync(
                user,
                request.PhoneNumber,
                request.Role,
                cancellationToken);

            if (!created)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<CreateEmployeeResponse>(DomainErrors.Employee.CreationFailed);
            }

            var employee = new Employee
            {
                Id = employeeId,
                CompanyId = request.CompanyId,
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                EmployeeCode = employeeCode,
                EmploymentStatus = EmploymentStatus.Active,
                UserId = user.Id
            };

            await _unitOfWork.Employees.AddAsync(employee, cancellationToken);

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
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<CreateEmployeeResponse>(DomainErrors.Employee.AlreadyExists);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<CreateEmployeeResponse>(DomainErrors.General.ServerError);
        }
    }
}