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
        // Leadership assignment is now handled via OrgNode assignments.
        // This method is a no-op for the new simplified role model.
        return Result.Success();
    }
}
