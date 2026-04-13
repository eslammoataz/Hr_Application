using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Services;

public class EmployeePlacementService : IEmployeePlacementService
{
    private readonly IUnitOfWork _unitOfWork;

    public EmployeePlacementService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<(Guid? DepartmentId, Guid? UnitId, Guid? TeamId)>> ResolvePlacementAsync(
        Guid companyId,
        Guid? departmentId,
        Guid? unitId,
        Guid? teamId,
        CancellationToken cancellationToken)
    {
        Department? department = null;
        Guid? resolvedDepartmentId = null;
        Guid? resolvedUnitId = null;
        Guid? resolvedTeamId = null;

        if (teamId.HasValue)
        {
            var team = await _unitOfWork.Teams.GetByIdAsync(teamId.Value, cancellationToken);
            if (team == null)
                return Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.Team.NotFound);

            if (unitId.HasValue && unitId.Value != team.UnitId)
                return Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.General.InvalidOperation);

            resolvedTeamId = team.Id;
            resolvedUnitId = team.UnitId;
        }

        if (resolvedUnitId.HasValue || unitId.HasValue)
        {
            var unitIdToUse = resolvedUnitId ?? unitId!.Value;
            var unit = await _unitOfWork.Units.GetByIdAsync(unitIdToUse, cancellationToken);
            if (unit == null)
                return Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.Unit.NotFound);

            if (departmentId.HasValue && departmentId.Value != unit.DepartmentId)
                return Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.General.InvalidOperation);

            resolvedUnitId = unit.Id;
            resolvedDepartmentId = unit.DepartmentId;
        }

        if (resolvedDepartmentId.HasValue || departmentId.HasValue)
        {
            var deptIdToUse = resolvedDepartmentId ?? departmentId!.Value;
            department = await _unitOfWork.Departments.GetByIdAsync(deptIdToUse, cancellationToken);
            if (department == null)
                return Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.Department.NotFound);

            if (department.CompanyId != companyId)
                return Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.General.InvalidOperation);

            resolvedDepartmentId = department.Id;
        }

        return Result.Success((resolvedDepartmentId, resolvedUnitId, resolvedTeamId));
    }

    public async Task<Result> AssignLeadershipIfNeededAsync(
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
