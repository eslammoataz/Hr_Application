using HrSystemApp.Application.DTOs.Hierarchy;
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
        return positions.OrderBy(p => p.SortOrder).Select(p => p.Role).ToList();
    }

    public async Task<bool> AreRolesValidForCompanyAsync(Guid companyId, IEnumerable<UserRole> roles,
        CancellationToken ct = default)
    {
        var available = await GetAvailableRolesAsync(companyId, ct);
        return roles.All(r => available.Contains(r));
    }

    public async Task<List<Employee>> GetEmployeeHierarchyPathAsync(Guid employeeId, CancellationToken ct = default)
    {
        var path = new List<Employee>();
        var current = await _unitOfWork.Employees.GetByIdAsync(employeeId, ct);

        while (current?.ManagerId != null)
        {
            current = await _unitOfWork.Employees.GetByIdAsync(current.ManagerId.Value, ct);
            if (current != null) path.Add(current);
        }

        return path;
    }

    public async Task<List<(Guid Id, string Type)>> GetHierarchyChildrenAsync(Guid companyId, Guid parentId,
        string parentType, CancellationToken ct = default)
    {
        var result = new List<(Guid Id, string Type)>();

        switch (parentType.ToLower())
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
                var depts = await _unitOfWork.Departments.FindAsync(d => d.VicePresidentId == parentId || d.ManagerId == parentId, ct);
                var units = await _unitOfWork.Units.FindAsync(u => u.UnitLeaderId == parentId, ct);
                var teams = await _unitOfWork.Teams.FindAsync(t => t.TeamLeaderId == parentId, ct);

                foreach (var d in depts) result.Add((d.Id, "Department"));
                foreach (var u in units) result.Add((u.Id, "Unit"));
                foreach (var t in teams) result.Add((t.Id, "Team"));

                // 2. Direct reports
                var directReports = await _unitOfWork.Employees.FindAsync(e => e.ManagerId == parentId, ct);
                
                var leadersToExclude = new HashSet<Guid>();
                // Exclude leaders of DEPARTMENTS being unboxed
                foreach (var d in depts)
                {
                    if (d.ManagerId.HasValue && d.ManagerId != parentId) 
                        leadersToExclude.Add(d.ManagerId.Value);
                }
                // Exclude leaders of UNITS being unboxed
                foreach (var u in units)
                {
                    if (u.UnitLeaderId.HasValue && u.UnitLeaderId != parentId) 
                        leadersToExclude.Add(u.UnitLeaderId.Value);
                }
                // Exclude leaders of TEAMS being unboxed
                foreach (var t in teams)
                {
                    if (t.TeamLeaderId.HasValue && t.TeamLeaderId != parentId) 
                        leadersToExclude.Add(t.TeamLeaderId.Value);
                }

                foreach (var emp in directReports)
                {
                    if (!leadersToExclude.Contains(emp.Id))
                        result.Add((emp.Id, "Employee"));
                }
                break;
        }

        return result.DistinctBy(x => x.Id).ToList();
    }

    public async Task<Dictionary<Guid, HierarchyNodeMetadata>> GetNodesMetadataAsync(IEnumerable<(Guid Id, string Type)> nodes,
        CancellationToken ct = default)
    {
        var results = new Dictionary<Guid, HierarchyNodeMetadata>();
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
                results[emp.Id] = new HierarchyNodeMetadata
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
                results[d.Id] = new HierarchyNodeMetadata
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
                results[u.Id] = new HierarchyNodeMetadata
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
                results[t.Id] = new HierarchyNodeMetadata
                {
                    Name = t.Name,
                    LeaderName = t.TeamLeader?.FullName,
                    HasChildren = t.TeamLeaderId.HasValue
                };
            }
        }

        return results;
    }

    private async Task<List<Guid>> GetEmployeesWithChildrenAsync(List<Guid> employeeIds, CancellationToken ct)
    {
        var hasChildrenIds = new List<Guid>();

        // 1. Check if they are managers of others
        var childReports = await _unitOfWork.Employees.FindAsync(e => e.ManagerId.HasValue && employeeIds.Contains(e.ManagerId.Value), ct);
        hasChildrenIds.AddRange(childReports.Select(e => e.ManagerId!.Value));

        // 2. Check if they lead organizations
        var vps = await _unitOfWork.Departments.FindAsync(d => d.VicePresidentId.HasValue && employeeIds.Contains(d.VicePresidentId.Value), ct);
        hasChildrenIds.AddRange(vps.Select(d => d.VicePresidentId!.Value));

        var deptManagers = await _unitOfWork.Departments.FindAsync(d => d.ManagerId.HasValue && employeeIds.Contains(d.ManagerId.Value), ct);
        hasChildrenIds.AddRange(deptManagers.Select(d => d.ManagerId!.Value));

        var unitLeaders = await _unitOfWork.Units.FindAsync(u => u.UnitLeaderId.HasValue && employeeIds.Contains(u.UnitLeaderId.Value), ct);
        hasChildrenIds.AddRange(unitLeaders.Select(u => u.UnitLeaderId!.Value));

        var teamLeaders = await _unitOfWork.Teams.FindAsync(t => t.TeamLeaderId.HasValue && employeeIds.Contains(t.TeamLeaderId.Value), ct);
        hasChildrenIds.AddRange(teamLeaders.Select(t => t.TeamLeaderId!.Value));

        return hasChildrenIds.Distinct().ToList();
    }
}
