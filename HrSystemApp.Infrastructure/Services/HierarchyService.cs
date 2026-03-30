using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.AspNetCore.Identity;

namespace HrSystemApp.Infrastructure.Services;

public class HierarchyService : IHierarchyService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;

    public HierarchyService(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<List<UserRole>> GetAvailableRolesAsync(Guid companyId, CancellationToken ct = default)
    {
        var positions = await _unitOfWork.HierarchyPositions.GetByCompanyAsync(companyId, ct);
        return positions.Select(p => p.Role).ToList();
    }

    public async Task<bool> AreRolesValidForCompanyAsync(Guid companyId, IEnumerable<UserRole> roles,
        CancellationToken ct = default)
    {
        var availableRoles = await GetAvailableRolesAsync(companyId, ct);
        return roles.All(r => availableRoles.Contains(r));
    }

    public async Task<List<Employee>> GetEmployeeHierarchyPathAsync(Guid employeeId, CancellationToken ct = default)
    {
        var path = new List<Employee>();
        var employee = await _unitOfWork.Employees.GetByIdAsync(employeeId, ct);
        if (employee == null) return path;

        // 1. Team Leader
        if (employee.TeamId.HasValue)
        {
            var team = await _unitOfWork.Teams.GetByIdAsync(employee.TeamId.Value, ct);
            if (team?.TeamLeaderId.HasValue == true && team.TeamLeaderId != employeeId)
            {
                var leader = await _unitOfWork.Employees.GetByIdAsync(team.TeamLeaderId.Value, ct);
                if (leader != null) path.Add(leader);
            }
        }

        // 2. Unit Leader
        if (employee.UnitId.HasValue)
        {
            var unit = await _unitOfWork.Units.GetByIdAsync(employee.UnitId.Value, ct);
            if (unit?.UnitLeaderId.HasValue == true && unit.UnitLeaderId != employeeId)
            {
                var leader = await _unitOfWork.Employees.GetByIdAsync(unit.UnitLeaderId.Value, ct);
                if (leader != null && !path.Any(e => e.Id == leader.Id)) path.Add(leader);
            }
        }

        // 3. Department Manager
        if (employee.DepartmentId.HasValue)
        {
            var dept = await _unitOfWork.Departments.GetByIdAsync(employee.DepartmentId.Value, ct);
            if (dept?.ManagerId.HasValue == true && dept.ManagerId != employeeId)
            {
                var manager = await _unitOfWork.Employees.GetByIdAsync(dept.ManagerId.Value, ct);
                if (manager != null && !path.Any(e => e.Id == manager.Id)) path.Add(manager);
            }

            // 4. Vice President
            if (dept?.VicePresidentId.HasValue == true && dept.VicePresidentId != employeeId)
            {
                var vp = await _unitOfWork.Employees.GetByIdAsync(dept.VicePresidentId.Value, ct);
                if (vp != null && !path.Any(e => e.Id == vp.Id)) path.Add(vp);
            }
        }

        // 5. CEO (Find the employee in this company with CEO identity role)
        var ceoUsers = await _userManager.GetUsersInRoleAsync(UserRole.CEO.ToString());
        var ceoUserIds = ceoUsers.Select(u => u.Id).ToHashSet();
        var ceoEmployees = await _unitOfWork.Employees.FindAsync(
            e => e.CompanyId == employee.CompanyId && !e.IsDeleted && e.UserId != null &&
                 ceoUserIds.Contains(e.UserId!),
            ct);

        var ceo = ceoEmployees.FirstOrDefault();
        if (ceo != null && ceo.Id != employeeId && !path.Any(e => e.Id == ceo.Id))
        {
            path.Add(ceo);
        }

        return path;
    }
}
