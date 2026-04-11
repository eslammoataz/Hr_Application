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

        var placementResult = await ResolvePlacementAsync(request, cancellationToken);
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
            var leadershipAssignmentResult = await AssignLeadershipIfNeededAsync(employee, request.Role, cancellationToken);
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

    private async Task<Result<(Guid? DepartmentId, Guid? UnitId, Guid? TeamId)>> ResolvePlacementAsync(
        CreateEmployeeCommand request,
        CancellationToken cancellationToken)
    {
        Department? department = null;
        Guid? resolvedDepartmentId = null;
        Guid? resolvedUnitId = null;
        Guid? resolvedTeamId = null;

        if (request.TeamId.HasValue)
        {
            var team = await _unitOfWork.Teams.GetByIdAsync(request.TeamId.Value, cancellationToken);
            if (team == null)
                return Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.Team.NotFound);

            if (request.UnitId.HasValue && request.UnitId.Value != team.UnitId)
                return Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.General.InvalidOperation);

            resolvedTeamId = team.Id;
            resolvedUnitId = team.UnitId;
        }

        if (resolvedUnitId.HasValue || request.UnitId.HasValue)
        {
            var unitIdToUse = resolvedUnitId ?? request.UnitId!.Value;
            var unit = await _unitOfWork.Units.GetByIdAsync(unitIdToUse, cancellationToken);
            if (unit == null)
                return Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.Unit.NotFound);

            if (request.DepartmentId.HasValue && request.DepartmentId.Value != unit.DepartmentId)
                return Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.General.InvalidOperation);

            resolvedUnitId = unit.Id;
            resolvedDepartmentId = unit.DepartmentId;
        }

        if (resolvedDepartmentId.HasValue || request.DepartmentId.HasValue)
        {
            var deptIdToUse = resolvedDepartmentId ?? request.DepartmentId!.Value;
            department = await _unitOfWork.Departments.GetByIdAsync(deptIdToUse, cancellationToken);
            if (department == null)
                return Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.Department.NotFound);

            if (department.CompanyId != request.CompanyId)
                return Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.General.InvalidOperation);

            resolvedDepartmentId = department.Id;
        }

        return Result.Success((resolvedDepartmentId, resolvedUnitId, resolvedTeamId));
    }

    private async Task<Result> AssignLeadershipIfNeededAsync(
        Employee employee,
        UserRole role,
        CancellationToken cancellationToken)
    {
        Guid? oldLeaderId = null;

        switch (role)
        {
            case UserRole.TeamLeader:
                if (!employee.TeamId.HasValue)
                    return Result.Failure(DomainErrors.Team.NotFound);

                var team = await _unitOfWork.Teams.GetByIdAsync(employee.TeamId.Value, cancellationToken);
                if (team == null)
                    return Result.Failure(DomainErrors.Team.NotFound);

                oldLeaderId = team.TeamLeaderId;
                team.TeamLeaderId = employee.Id;
                break;

            case UserRole.UnitLeader:
                if (!employee.UnitId.HasValue)
                    return Result.Failure(DomainErrors.Unit.NotFound);

                var unit = await _unitOfWork.Units.GetByIdAsync(employee.UnitId.Value, cancellationToken);
                if (unit == null)
                    return Result.Failure(DomainErrors.Unit.NotFound);

                oldLeaderId = unit.UnitLeaderId;
                unit.UnitLeaderId = employee.Id;
                break;

            case UserRole.DepartmentManager:
                if (!employee.DepartmentId.HasValue)
                    return Result.Failure(DomainErrors.Department.NotFound);

                var departmentForManager =
                    await _unitOfWork.Departments.GetByIdAsync(employee.DepartmentId.Value, cancellationToken);
                if (departmentForManager == null)
                    return Result.Failure(DomainErrors.Department.NotFound);

                oldLeaderId = departmentForManager.ManagerId;
                departmentForManager.ManagerId = employee.Id;
                break;

            case UserRole.VicePresident:
                if (!employee.DepartmentId.HasValue)
                    return Result.Failure(DomainErrors.Department.NotFound);

                var departmentForVp =
                    await _unitOfWork.Departments.GetByIdAsync(employee.DepartmentId.Value, cancellationToken);
                if (departmentForVp == null)
                    return Result.Failure(DomainErrors.Department.NotFound);

                oldLeaderId = departmentForVp.VicePresidentId;
                departmentForVp.VicePresidentId = employee.Id;
                break;
        }

        return await DemoteOldLeaderIfNeededAsync(oldLeaderId, role, cancellationToken);
    }

    private async Task<Result> DemoteOldLeaderIfNeededAsync(
        Guid? oldLeaderId,
        UserRole oldRole,
        CancellationToken cancellationToken)
    {
        if (!oldLeaderId.HasValue || oldLeaderId.Value == Guid.Empty)
            return Result.Success();

        var oldLeaderUser = (await _unitOfWork.Users.FindAsync(u => u.EmployeeId == oldLeaderId.Value, cancellationToken))
            .FirstOrDefault();

        if (oldLeaderUser == null)
            return Result.Success();

        var removed = await _unitOfWork.Users.RemoveFromRoleAsync(oldLeaderUser, oldRole.ToString(), cancellationToken);
        if (!removed)
            return Result.Failure(DomainErrors.General.InvalidOperation);

        var added = await _unitOfWork.Users.AddToRoleAsync(oldLeaderUser, UserRole.Employee.ToString(), cancellationToken);
        if (!added)
            return Result.Failure(DomainErrors.General.InvalidOperation);

        return Result.Success();
    }
}
