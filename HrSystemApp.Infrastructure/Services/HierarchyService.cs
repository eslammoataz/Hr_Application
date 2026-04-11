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

    /// <summary>
    /// Retrieve the hierarchy roles defined for a company, ordered by each position's SortOrder.
    /// </summary>
    /// <param name="companyId">The company identifier whose hierarchy positions to query.</param>
    /// <param name="ct">A cancellation token to cancel the operation.</param>
    /// <returns>A list of available <see cref="UserRole"/> values ordered by position <c>SortOrder</c>.</returns>
    public async Task<List<UserRole>> GetAvailableRolesAsync(Guid companyId, CancellationToken ct = default)
    {
        var positions = await _unitOfWork.HierarchyPositions.GetByCompanyAsync(companyId, ct);
        return positions.OrderBy(p => p.SortOrder).Select(p => p.Role).ToList();
    }

    /// <summary>
    /// Determines whether all specified roles are valid for the given company.
    /// </summary>
    /// <param name="companyId">The company identifier to validate roles against.</param>
    /// <param name="roles">The collection of roles to validate.</param>
    /// <returns>`true` if every role in <paramref name="roles"/> exists among the company's available roles, `false` otherwise.</returns>
    public async Task<bool> AreRolesValidForCompanyAsync(Guid companyId, IEnumerable<UserRole> roles,
        CancellationToken ct = default)
    {
        var available = await GetAvailableRolesAsync(companyId, ct);
        return roles.All(r => available.Contains(r));
    }

    /// <summary>
    /// Build the management chain for the given employee, walking upward from the employee's direct manager to the top-level manager.
    /// </summary>
    /// <param name="employeeId">The identifier of the employee whose manager chain is requested.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A list of Employee objects representing the management chain ordered from the direct manager upward; empty if the employee has no manager or the employee is not found.</returns>
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

    /// <summary>
    /// Gets the immediate child nodes for a hierarchy node identified by <paramref name="parentId"/> and <paramref name="parentType"/>.
    /// </summary>
    /// <param name="companyId">The company identifier used to scope the hierarchy query.</param>
    /// <param name="parentId">The identifier of the parent node whose children are requested.</param>
    /// <param name="parentType">The parent node type: "Department", "Unit", "Team", or "Employee".</param>
    /// <returns>A list of distinct tuples where the first item is the child node Id and the second item is the child node Type ("Employee", "Department", "Unit", or "Team").</returns>
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

    /// <summary>
    /// Builds metadata for the provided hierarchy nodes.
    /// </summary>
    /// <param name="nodes">A collection of node specs where each item is a tuple of (Id, Type). Supported Type values are "Employee", "Department", "Unit", and "Team".</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A dictionary mapping each found node Id to its HierarchyNodeMetadata. Nodes that do not exist in the data store are omitted. For employee nodes, the returned metadata includes whether the employee has children.</returns>
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

    /// <summary>
    /// Determines which of the specified employees should be marked as having children in the hierarchy.
    /// </summary>
    /// <param name="employeeIds">Employee IDs to evaluate for having children.</param>
    /// <returns>A distinct list of employee IDs that have at least one direct report or lead at least one department, unit, or team.</returns>
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
