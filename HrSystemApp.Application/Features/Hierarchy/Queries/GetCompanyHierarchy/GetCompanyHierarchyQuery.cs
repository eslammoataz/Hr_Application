using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using DomainUnit = HrSystemApp.Domain.Models.Unit;

namespace HrSystemApp.Application.Features.Hierarchy.Queries.GetCompanyHierarchy;

public record HierarchyMetadata(
    string? Email = null,
    string? EmployeeCode = null,
    int? OccupantCount = null,
    string? ManagerName = null,
    Guid? ManagerId = null);

public record HierarchyNodeDto(
    Guid Id,
    Guid? ParentId,
    string Name,
    string NodeType,       // "Employee", "Department", "Unit", "Team", "PositionLevel"
    string? Role = null,
    string? PositionTitle = null,
    bool HasChildren = false,
    HierarchyMetadata? Metadata = null);

public record CompanyHierarchyDto(
    Guid CompanyId,
    string CompanyName,
    IReadOnlyList<HierarchyNodeDto> Nodes);

public record GetCompanyHierarchyQuery(Guid? ParentId = null, string? ParentType = null)
    : IRequest<Result<CompanyHierarchyDto>>;

public class GetCompanyHierarchyQueryHandler : IRequestHandler<GetCompanyHierarchyQuery, Result<CompanyHierarchyDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetCompanyHierarchyQueryHandler> _logger;

    public GetCompanyHierarchyQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<GetCompanyHierarchyQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<CompanyHierarchyDto>> Handle(GetCompanyHierarchyQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<CompanyHierarchyDto>(DomainErrors.Auth.Unauthorized);

        var currentEmployee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (currentEmployee == null)
            return Result.Failure<CompanyHierarchyDto>(DomainErrors.Employee.NotFound);

        var companyId = currentEmployee.CompanyId;
        var company = await _unitOfWork.Companies.GetByIdAsync(companyId, cancellationToken);
        if (company == null)
            return Result.Failure<CompanyHierarchyDto>(DomainErrors.Company.NotFound);

        var nodes = new List<HierarchyNodeDto>();

        if (request.ParentId == null)
        {
            // Initial Load: Discover Roots
            nodes.AddRange(await GetRootsAsync(companyId, cancellationToken));
        }
        else
        {
            // Expansion Load: Discover Children
            nodes.AddRange(await GetChildrenAsync(companyId, request.ParentId.Value, request.ParentType, cancellationToken));
        }

        return Result.Success(new CompanyHierarchyDto(companyId, company.CompanyName, nodes));
    }

    private async Task<List<HierarchyNodeDto>> GetRootsAsync(Guid companyId, CancellationToken ct)
    {
        var nodes = new List<HierarchyNodeDto>();

        // 1. Get ordered positions to understand role hierarchy
        var positions = await _unitOfWork.HierarchyPositions.GetByCompanyAsync(companyId, ct);
        if (!positions.Any()) return nodes;

        // 2. Load all employees for the company
        var allEmployees = await _unitOfWork.Employees.GetByCompanyAsync(companyId, ct);
        if (!allEmployees.Any()) return nodes;

        // 3. Find roles for candidates
        var userIds = allEmployees.Where(e => e.UserId != null).Select(e => e.UserId!).ToList();
        var roles = await _unitOfWork.Users.GetPrimaryRolesByUserIdsAsync(userIds, ct);

        // STAGE 1: Strict match - Highest configured role AND no manager
        var topRole = positions.OrderBy(p => p.SortOrder).First();
        var rootEmployees = allEmployees
            .Where(e => e.ManagerId == null &&
                        e.UserId != null &&
                        roles.TryGetValue(e.UserId, out var role) &&
                        string.Equals(role, topRole.Role.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (rootEmployees.Any())
        {
            foreach (var emp in rootEmployees)
            {
                nodes.Add(await MapEmployeeNodeAsync(emp, null, topRole.PositionTitle, ct));
            }
            return nodes;
        }

        // STAGE 2: Fallback - Highest role found among ANY unmanaged employees
        var unmanagedEmployees = allEmployees.Where(e => e.ManagerId == null).ToList();
        if (unmanagedEmployees.Any())
        {
            var rankedCandidates = unmanagedEmployees
                .Select(e =>
                {
                    var roleName = roles.GetValueOrDefault(e.UserId ?? "", "");
                    var position = positions.FirstOrDefault(p => string.Equals(p.Role.ToString(), roleName, StringComparison.OrdinalIgnoreCase));
                    return new { Employee = e, Position = position, RoleName = roleName };
                })
                .OrderBy(c => c.Position?.SortOrder ?? int.MaxValue)
                .ToList();

            var topSortOrder = rankedCandidates.First().Position?.SortOrder ?? int.MaxValue;
            var finalRoots = rankedCandidates
                .Where(c => (c.Position?.SortOrder ?? int.MaxValue) == topSortOrder)
                .ToList();

            foreach (var candidate in finalRoots)
            {
                var posTitle = candidate.Position?.PositionTitle ?? "Staff";
                nodes.Add(await MapEmployeeNodeAsync(candidate.Employee, null, posTitle, ct));
            }
        }

        // STAGE 3: Final Fallback - Unmanaged departments
        if (!nodes.Any())
        {
            var depts = await _unitOfWork.Departments.GetByCompanyAsync(companyId, ct);
            foreach (var dept in depts.Where(d => d.VicePresidentId == null && d.ManagerId == null))
            {
                nodes.Add(await MapDepartmentNodeAsync(dept, null, ct));
            }
        }

        return nodes;
    }

    private async Task<List<HierarchyNodeDto>> GetChildrenAsync(Guid companyId, Guid parentId, string? parentType, CancellationToken ct)
    {
        return parentType?.ToLower() switch
        {
            "employee" => await GetEmployeeChildrenAsync(companyId, parentId, ct),
            "department" => await GetDepartmentChildrenAsync(companyId, parentId, ct),
            "unit" => await GetUnitChildrenAsync(companyId, parentId, ct),
            "team" => await GetTeamChildrenAsync(companyId, parentId, ct),
            _ => new List<HierarchyNodeDto>()
        };
    }

    private async Task<List<HierarchyNodeDto>> GetEmployeeChildrenAsync(Guid companyId, Guid employeeId, CancellationToken ct)
    {
        var nodes = new List<HierarchyNodeDto>();
        var positions = await _unitOfWork.HierarchyPositions.FindAsync(p => p.CompanyId == companyId && !p.IsDeleted, ct);
        var rankMap = positions.ToDictionary(p => p.Role.ToString(), p => p.SortOrder, StringComparer.OrdinalIgnoreCase);

        // 1. ORGANIZATIONAL DISCOVERY (Leaders "unbox" the next level)
        
        // If Manager of a Department -> Return the Units in that Department
        var managedDepts = await _unitOfWork.Departments.FindAsync(d => d.ManagerId == employeeId && !d.IsDeleted, ct);
        foreach (var dept in managedDepts)
        {
            var units = await _unitOfWork.Units.GetByDepartmentIdsAsync(new[] { dept.Id }, ct);
            foreach (var unit in units)
                nodes.Add(await MapUnitNodeAsync(unit, employeeId, ct));
                
            // Also find employees directly in this department (not in a unit)
            var deptEmps = await _unitOfWork.Employees.FindAsync(e => e.DepartmentId == dept.Id && e.UnitId == null && e.Id != employeeId && !e.IsDeleted, ct);
            foreach (var emp in deptEmps)
                nodes.Add(await MapEmployeeNodeAsync(emp, employeeId, null, ct));
        }

        // If Leader of a Unit -> Return the Teams in that Unit
        var ledUnits = await _unitOfWork.Units.FindAsync(u => u.UnitLeaderId == employeeId && !u.IsDeleted, ct);
        foreach (var unit in ledUnits)
        {
            var teams = await _unitOfWork.Teams.GetByUnitIdsAsync(new[] { unit.Id }, ct);
            foreach (var team in teams)
                nodes.Add(await MapTeamNodeAsync(team, employeeId, ct));
                
            // Also find employees directly in this unit (not in a team)
            var unitEmps = await _unitOfWork.Employees.FindAsync(e => e.UnitId == unit.Id && e.TeamId == null && e.Id != employeeId && !e.IsDeleted, ct);
            foreach (var emp in unitEmps)
                nodes.Add(await MapEmployeeNodeAsync(emp, employeeId, null, ct));
        }

        // If Leader of a Team -> Return the Employees in that Team
        var ledTeams = await _unitOfWork.Teams.FindAsync(t => t.TeamLeaderId == employeeId && !t.IsDeleted, ct);
        foreach (var team in ledTeams)
        {
            var teamEmps = await _unitOfWork.Employees.FindAsync(e => e.TeamId == team.Id && e.Id != employeeId && !e.IsDeleted, ct);
            foreach (var emp in teamEmps)
                nodes.Add(await MapEmployeeNodeAsync(emp, employeeId, null, ct));
        }

        // If VP of a Department -> Return the Department itself
        var vpDepts = await _unitOfWork.Departments.FindAsync(d => d.VicePresidentId == employeeId && !d.IsDeleted, ct);
        foreach (var dept in vpDepts)
            nodes.Add(await MapDepartmentNodeAsync(dept, employeeId, ct));

        // 2. DIRECT REPORTS (Fallback for any other reporting lines)
        var reports = await _unitOfWork.Employees.FindAsync(e => e.ManagerId == employeeId && !e.IsDeleted, ct);
        
        // Identify leaders of the organizations we just added to the nodes list to prevent "Double Discovery"
        var leadersToExclude = new HashSet<Guid>();
        
        // Exclude Managers of Departments this person leads/VPs
        var deptsToProbe = managedDepts.Concat(vpDepts).DistinctBy(d => d.Id);
        foreach (var d in deptsToProbe) if (d.ManagerId.HasValue) leadersToExclude.Add(d.ManagerId.Value);
        
        // Exclude Unit Leaders of Units this person "unboxed" (when Manager leads a Dept)
        foreach (var d in managedDepts)
        {
            var units = await _unitOfWork.Units.GetByDepartmentIdsAsync(new[] { d.Id }, ct);
            foreach (var u in units) if (u.UnitLeaderId.HasValue) leadersToExclude.Add(u.UnitLeaderId.Value);
        }

        // Exclude Unit Leaders of Units this person leads directly
        foreach (var u in ledUnits) if (u.UnitLeaderId.HasValue) leadersToExclude.Add(u.UnitLeaderId.Value);

        // Exclude Team Leaders of Teams this person "unboxed" (when Unit Leader leads a Unit)
        foreach (var u in ledUnits)
        {
            var teams = await _unitOfWork.Teams.GetByUnitIdsAsync(new[] { u.Id }, ct);
            foreach (var t in teams) if (t.TeamLeaderId.HasValue) leadersToExclude.Add(t.TeamLeaderId.Value);
        }

        // Exclude Team Leaders of Teams this person leads directly
        foreach (var t in ledTeams) if (t.TeamLeaderId.HasValue) leadersToExclude.Add(t.TeamLeaderId.Value);

        // Filter: If they are the Leader of an Org just mapped, or part of a sub-org expansion above, skip
        var currentIds = nodes.Select(n => n.Id).ToHashSet();
        foreach (var report in reports)
        {
            if (currentIds.Contains(report.Id)) continue;
            if (leadersToExclude.Contains(report.Id)) continue;

            nodes.Add(await MapEmployeeNodeAsync(report, employeeId, null, ct));
        }

        return nodes.DistinctBy(x => x.Id)
            .OrderBy(n => n.NodeType switch { "Department" => 1, "Unit" => 2, "Team" => 3, _ => 4 })
            .ThenBy(n => n.Role != null && rankMap.TryGetValue(n.Role, out var rank) ? rank : 999)
            .ToList();
    }

    private async Task<List<HierarchyNodeDto>> GetDepartmentChildrenAsync(Guid companyId, Guid deptId, CancellationToken ct)
    {
        var nodes = new List<HierarchyNodeDto>();
        var dept = await _unitOfWork.Departments.GetByIdAsync(deptId);
        if (dept == null || !dept.ManagerId.HasValue) return nodes;

        // Departmet expands ONLY to its primary leader (Manager)
        var manager = await _unitOfWork.Employees.GetByIdAsync(dept.ManagerId.Value);
        if (manager != null && !manager.IsDeleted)
            nodes.Add(await MapEmployeeNodeAsync(manager, deptId, null, ct));

        return nodes;
    }

    private async Task<List<HierarchyNodeDto>> GetUnitChildrenAsync(Guid companyId, Guid unitId, CancellationToken ct)
    {
        var nodes = new List<HierarchyNodeDto>();
        var unit = await _unitOfWork.Units.GetByIdAsync(unitId);
        if (unit == null || !unit.UnitLeaderId.HasValue) return nodes;

        // Unit expands ONLY to its primary leader (Unit Leader)
        var leader = await _unitOfWork.Employees.GetByIdAsync(unit.UnitLeaderId.Value);
        if (leader != null && !leader.IsDeleted)
            nodes.Add(await MapEmployeeNodeAsync(leader, unitId, null, ct));

        return nodes;
    }

    private async Task<List<HierarchyNodeDto>> GetTeamChildrenAsync(Guid companyId, Guid teamId, CancellationToken ct)
    {
        var nodes = new List<HierarchyNodeDto>();
        var team = await _unitOfWork.Teams.GetByIdAsync(teamId);
        if (team == null || !team.TeamLeaderId.HasValue) return nodes;

        // Team expands ONLY to its primary leader (Team Leader)
        var leader = await _unitOfWork.Employees.GetByIdAsync(team.TeamLeaderId.Value);
        if (leader != null && !leader.IsDeleted)
            nodes.Add(await MapEmployeeNodeAsync(leader, teamId, null, ct));

        return nodes;
    }

    private async Task<HierarchyNodeDto> MapEmployeeNodeAsync(Employee emp, Guid? parentId, string? posTitle, CancellationToken ct)
    {
        var roles = await _unitOfWork.Users.GetPrimaryRolesByUserIdsAsync(new[] { emp.UserId ?? "" }, ct);
        var role = roles.GetValueOrDefault(emp.UserId ?? "", "");

        var hasChildren = await _unitOfWork.Employees.ExistsAsync(e => e.ManagerId == emp.Id && !e.IsDeleted, ct)
            || await _unitOfWork.Departments.ExistsAsync(d => d.VicePresidentId == emp.Id || d.ManagerId == emp.Id, ct)
            || await _unitOfWork.Units.ExistsAsync(u => u.UnitLeaderId == emp.Id, ct)
            || await _unitOfWork.Teams.ExistsAsync(t => t.TeamLeaderId == emp.Id, ct);

        return new HierarchyNodeDto(
            emp.Id,
            parentId,
            emp.FullName,
            "Employee",
            role,
            posTitle ?? role,
            hasChildren,
            new HierarchyMetadata(emp.Email, emp.EmployeeCode, null, emp.Manager?.FullName, emp.ManagerId));
    }

    private async Task<HierarchyNodeDto> MapDepartmentNodeAsync(Department dept, Guid? parentId, CancellationToken ct)
    {
        var hasChildren = dept.ManagerId.HasValue;

        return new HierarchyNodeDto(
            dept.Id,
            parentId,
            dept.Name,
            "Department",
            null, null,
            hasChildren,
            new HierarchyMetadata(null, null, null, dept.VicePresident?.FullName ?? dept.Manager?.FullName));
    }

    private async Task<HierarchyNodeDto> MapUnitNodeAsync(DomainUnit unit, Guid? parentId, CancellationToken ct)
    {
        var hasChildren = unit.UnitLeaderId.HasValue;

        return new HierarchyNodeDto(
            unit.Id,
            parentId,
            unit.Name,
            "Unit",
            null, null,
            hasChildren,
            new HierarchyMetadata(null, null, null, unit.UnitLeader?.FullName));
    }

    private async Task<HierarchyNodeDto> MapTeamNodeAsync(Team team, Guid? parentId, CancellationToken ct)
    {
        var hasChildren = team.TeamLeaderId.HasValue;

        return new HierarchyNodeDto(
            team.Id,
            parentId,
            team.Name,
            "Team",
            null, null,
            hasChildren,
            new HierarchyMetadata(null, null, null, team.TeamLeader?.FullName));
    }

}
