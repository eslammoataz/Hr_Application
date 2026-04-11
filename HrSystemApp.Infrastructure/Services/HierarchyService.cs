using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
        
        // Eager load everything needed for the path to avoid N+1
        var employee = await _unitOfWork.Employees.FindAsync(e => e.Id == employeeId && !e.IsDeleted, ct, 
            e => e.Team!, 
            e => e.Unit!, 
            e => e.Department!);
        
        var emp = employee.FirstOrDefault();
        if (emp == null) return path;

        // 1. Team Leader
        if (emp.TeamId.HasValue && emp.Team != null)
        {
            if (emp.Team.TeamLeaderId.HasValue && emp.Team.TeamLeaderId != employeeId)
            {
                var leader = await _unitOfWork.Employees.GetByIdAsync(emp.Team.TeamLeaderId.Value, ct);
                if (leader != null) path.Add(leader);
            }
        }

        // 2. Unit Leader
        if (emp.UnitId.HasValue && emp.Unit != null)
        {
            if (emp.Unit.UnitLeaderId.HasValue && emp.Unit.UnitLeaderId != employeeId)
            {
                var leader = await _unitOfWork.Employees.GetByIdAsync(emp.Unit.UnitLeaderId.Value, ct);
                if (leader != null && !path.Any(e => e.Id == leader.Id)) path.Add(leader);
            }
        }

        // 3. Department Manager
        if (emp.DepartmentId.HasValue && emp.Department != null)
        {
            if (emp.Department.ManagerId.HasValue && emp.Department.ManagerId != employeeId)
            {
                var manager = await _unitOfWork.Employees.GetByIdAsync(emp.Department.ManagerId.Value, ct);
                if (manager != null && !path.Any(e => e.Id == manager.Id)) path.Add(manager);
            }

            // 4. Vice President
            if (emp.Department.VicePresidentId.HasValue && emp.Department.VicePresidentId != employeeId)
            {
                var vp = await _unitOfWork.Employees.GetByIdAsync(emp.Department.VicePresidentId.Value, ct);
                if (vp != null && !path.Any(e => e.Id == vp.Id)) path.Add(vp);
            }
        }

        // 5. CEO (Find the employee in this company with CEO identity role)
        var ceoUsers = await _userManager.GetUsersInRoleAsync("CEO");
        var ceoUserIds = ceoUsers.Select(u => u.Id).ToHashSet();
        var ceoEmployees = await _unitOfWork.Employees.FindAsync(
            e => e.CompanyId == emp.CompanyId && !e.IsDeleted && e.UserId != null &&
                 ceoUserIds.Contains(e.UserId!),
            ct);

        var ceo = ceoEmployees.FirstOrDefault();
        if (ceo != null && ceo.Id != employeeId && !path.Any(e => e.Id == ceo.Id))
        {
            path.Add(ceo);
        }

        return path;
    }

    public async Task<List<(Guid Id, string Type)>> GetHierarchyChildrenAsync(Guid companyId, Guid parentId,
        string parentType, CancellationToken ct = default)
    {
        var result = new List<(Guid Id, string Type)>();

        switch (parentType?.ToLower())
        {
            case "department":
                var dept = await _unitOfWork.Departments.GetByIdAsync(parentId, ct);
                if (dept?.ManagerId.HasValue == true)
                    result.Add((dept.ManagerId.Value, "Employee"));
                break;

            case "unit":
                var unit = await _unitOfWork.Units.GetByIdAsync(parentId, ct);
                if (unit?.UnitLeaderId.HasValue == true)
                    result.Add((unit.UnitLeaderId.Value, "Employee"));
                break;

            case "team":
                var team = await _unitOfWork.Teams.GetByIdAsync(parentId, ct);
                if (team?.TeamLeaderId.HasValue == true)
                    result.Add((team.TeamLeaderId.Value, "Employee"));
                break;

            case "employee":
                // 1. Unbox sub-organizations led by this person
                // VP -> Depts
                var vpDepts = await _unitOfWork.Departments.FindAsync(d => d.VicePresidentId == parentId && !d.IsDeleted, ct);
                foreach (var d in vpDepts) result.Add((d.Id, "Department"));

                // Manager -> Depts
                var managedDepts = await _unitOfWork.Departments.FindAsync(d => d.ManagerId == parentId && !d.IsDeleted, ct);
                foreach (var d in managedDepts) result.Add((d.Id, "Department"));

                // UL -> Units
                var ledUnits = await _unitOfWork.Units.FindAsync(u => u.UnitLeaderId == parentId && !u.IsDeleted, ct);
                foreach (var u in ledUnits) result.Add((u.Id, "Unit"));

                // TL -> Teams
                var ledTeams = await _unitOfWork.Teams.FindAsync(t => t.TeamLeaderId == parentId && !t.IsDeleted, ct);
                foreach (var t in ledTeams) result.Add((t.Id, "Team"));

                // 2. Direct reports (Reports to this person)
                var directReports = await _unitOfWork.Employees.FindAsync(e => e.ManagerId == parentId && !e.IsDeleted, ct);
                
                // Exclude people who were already "unboxed" as leaders of the above orgs to prevent Double Discovery
                var leadersToExclude = new HashSet<Guid>();
                
                // If this employee is a VP/Manager of a Dept, they unbox the Dept.
                // The Dept expands to the Manager.
                // The Manager then unboxes the UNITS in that Dept.
                // We must exclude the Unit Leaders from the Manager's direct reports list.
                foreach (var d in managedDepts)
                {
                    var subUnits = await _unitOfWork.Units.FindAsync(u => u.DepartmentId == d.Id && !u.IsDeleted, ct);
                    foreach (var u in subUnits) if (u.UnitLeaderId.HasValue) leadersToExclude.Add(u.UnitLeaderId.Value);
                }

                // If they lead a Unit, they unbox TEAMS in that Unit.
                // We must exclude the Team Leaders from the Unit Leader's direct reports list.
                foreach (var u in ledUnits)
                {
                    var subTeams = await _unitOfWork.Teams.FindAsync(t => t.UnitId == u.Id && !t.IsDeleted, ct);
                    foreach (var t in subTeams) if (t.TeamLeaderId.HasValue) leadersToExclude.Add(t.TeamLeaderId.Value);
                }

                // Also exclude leaders of orgs they lead directly
                foreach (var d in managedDepts) if (d.ManagerId.HasValue) leadersToExclude.Add(d.ManagerId.Value);
                foreach (var u in ledUnits) if (u.UnitLeaderId.HasValue) leadersToExclude.Add(u.UnitLeaderId.Value);
                foreach (var t in ledTeams) if (t.TeamLeaderId.HasValue) leadersToExclude.Add(t.TeamLeaderId.Value);

                foreach (var report in directReports)
                {
                    if (leadersToExclude.Contains(report.Id)) continue;
                    result.Add((report.Id, "Employee"));
                }
                break;
        }

        return result.DistinctBy(x => x.Id).ToList();
    }

    public async Task<Dictionary<Guid, dynamic>> GetNodesMetadataAsync(IEnumerable<(Guid Id, string Type)> nodes,
        CancellationToken ct = default)
    {
        var results = new Dictionary<Guid, dynamic>();
        var nodeSpecs = nodes.ToList();

        var employeeIds = nodeSpecs.Where(n => n.Type == "Employee").Select(n => n.Id).ToList();
        var deptIds = nodeSpecs.Where(n => n.Type == "Department").Select(n => n.Id).ToList();
        var unitIds = nodeSpecs.Where(n => n.Type == "Unit").Select(n => n.Id).ToList();
        var teamIds = nodeSpecs.Where(n => n.Type == "Team").Select(n => n.Id).ToList();

        // 1. Fetch Employees + Roles in batch
        if (employeeIds.Any())
        {
            var employees = await _unitOfWork.Employees.FindAsync(e => employeeIds.Contains(e.Id), ct);
            var roles = await _unitOfWork.Users.GetPrimaryRolesByUserIdsAsync(
                employees.Where(e => e.UserId != null).Select(e => e.UserId!).ToList(), ct);

            // Batch hasChildren check for employees
            var empHasChildrenIds = await GetEmployeesWithChildrenAsync(employeeIds, ct);

            foreach (var emp in employees)
            {
                var role = roles.GetValueOrDefault(emp.UserId ?? "", "");
                results[emp.Id] = new
                {
                    FullName = emp.FullName,
                    Role = role,
                    Email = emp.Email,
                    EmployeeCode = emp.EmployeeCode,
                    ManagerName = emp.Manager?.FullName,
                    ManagerId = emp.ManagerId,
                    HasChildren = empHasChildrenIds.Contains(emp.Id)
                };
            }
        }

        // 2. Fetch Organizations in batch
        if (deptIds.Any())
        {
            var depts = await _unitOfWork.Departments.FindAsync(d => deptIds.Contains(d.Id), ct);
            foreach (var d in depts)
            {
                results[d.Id] = new
                {
                    Name = d.Name,
                    LeaderName = d.Manager?.FullName ?? d.VicePresident?.FullName,
                    HasChildren = d.ManagerId.HasValue
                };
            }
        }

        if (unitIds.Any())
        {
            var units = await _unitOfWork.Units.FindAsync(u => unitIds.Contains(u.Id), ct);
            foreach (var u in units)
            {
                results[u.Id] = new
                {
                    Name = u.Name,
                    LeaderName = u.UnitLeader?.FullName,
                    HasChildren = u.UnitLeaderId.HasValue
                };
            }
        }

        if (teamIds.Any())
        {
            var teams = await _unitOfWork.Teams.FindAsync(t => teamIds.Contains(t.Id), ct);
            foreach (var t in teams)
            {
                results[t.Id] = new
                {
                    Name = t.Name,
                    LeaderName = t.TeamLeader?.FullName,
                    HasChildren = t.TeamLeaderId.HasValue
                };
            }
        }

        return results;
    }

    private async Task<HashSet<Guid>> GetEmployeesWithChildrenAsync(List<Guid> employeeIds, CancellationToken ct)
    {
        // Check if any of these employees is a Manager, VP, UL, or TL
        var managers = await _unitOfWork.Employees.FindAsync(e => e.ManagerId != null && employeeIds.Contains(e.ManagerId.Value), ct);
        var vps = await _unitOfWork.Departments.FindAsync(d => d.VicePresidentId != null && employeeIds.Contains(d.VicePresidentId.Value), ct);
        var deptMgrs = await _unitOfWork.Departments.FindAsync(d => d.ManagerId != null && employeeIds.Contains(d.ManagerId.Value), ct);
        var units = await _unitOfWork.Units.FindAsync(u => u.UnitLeaderId != null && employeeIds.Contains(u.UnitLeaderId.Value), ct);
        var teams = await _unitOfWork.Teams.FindAsync(t => t.TeamLeaderId != null && employeeIds.Contains(t.TeamLeaderId.Value), ct);

        var hasChildren = new HashSet<Guid>();
        foreach (var m in managers) if (m.ManagerId.HasValue) hasChildren.Add(m.ManagerId.Value);
        foreach (var v in vps) if (v.VicePresidentId.HasValue) hasChildren.Add(v.VicePresidentId.Value);
        foreach (var d in deptMgrs) if (d.ManagerId.HasValue) hasChildren.Add(d.ManagerId.Value);
        foreach (var u in units) if (u.UnitLeaderId.HasValue) hasChildren.Add(u.UnitLeaderId.Value);
        foreach (var t in teams) if (t.TeamLeaderId.HasValue) hasChildren.Add(t.TeamLeaderId.Value);

        return hasChildren;
    }
}
