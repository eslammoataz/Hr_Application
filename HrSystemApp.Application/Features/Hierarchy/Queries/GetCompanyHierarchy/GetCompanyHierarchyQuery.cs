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

        // 1. Organizations led by this person (PRIORITY)
        var ledDepts = await _unitOfWork.Departments.FindAsync(d => (d.VicePresidentId == employeeId || d.ManagerId == employeeId) && !d.IsDeleted, ct);
        foreach (var dept in ledDepts)
            nodes.Add(await MapDepartmentNodeAsync(dept, employeeId, ct));

        var ledUnits = await _unitOfWork.Units.FindAsync(u => u.UnitLeaderId == employeeId && !u.IsDeleted, ct);
        foreach (var unit in ledUnits)
            nodes.Add(await MapUnitNodeAsync(unit, employeeId, ct));

        var ledTeams = await _unitOfWork.Teams.FindAsync(t => t.TeamLeaderId == employeeId && !t.IsDeleted, ct);
        foreach (var team in ledTeams)
            nodes.Add(await MapTeamNodeAsync(team, employeeId, ct));

        // 2. Direct Reports (via ManagerId)
        // ALIGNMENT LOGIC: To match the "Structural First" view, we filter out reports 
        // who will already be discovered through the organizational nodes mapped above.
        var reports = await _unitOfWork.Employees.FindAsync(e => e.ManagerId == employeeId && !e.IsDeleted, ct);
        
        var ledDeptIds = ledDepts.Select(d => d.Id).ToHashSet();
        var ledUnitIds = ledUnits.Select(u => u.Id).ToHashSet();
        var ledTeamIds = ledTeams.Select(t => t.Id).ToHashSet();

        foreach (var report in reports)
        {
            // If the report is in an org that the parent leads, they will appear inside that Org node instead
            if (report.DepartmentId.HasValue && ledDeptIds.Contains(report.DepartmentId.Value)) continue;
            if (report.UnitId.HasValue && ledUnitIds.Contains(report.UnitId.Value)) continue;
            if (report.TeamId.HasValue && ledTeamIds.Contains(report.TeamId.Value)) continue;

            nodes.Add(await MapEmployeeNodeAsync(report, employeeId, null, ct));
        }

        return nodes.DistinctBy(x => x.Id).ToList();
    }

    private async Task<List<HierarchyNodeDto>> GetDepartmentChildrenAsync(Guid companyId, Guid deptId, CancellationToken ct)
    {
        var nodes = new List<HierarchyNodeDto>();

        // Units in Department
        var units = await _unitOfWork.Units.GetByDepartmentIdsAsync(new[] { deptId }, ct);
        foreach (var unit in units)
            nodes.Add(await MapUnitNodeAsync(unit, deptId, ct));

        // Direct Dept Employees
        var emps = await _unitOfWork.Employees.FindAsync(e => e.DepartmentId == deptId && e.UnitId == null && !e.IsDeleted, ct);
        foreach (var emp in emps)
            nodes.Add(await MapEmployeeNodeAsync(emp, deptId, null, ct));

        return nodes;
    }

    private async Task<List<HierarchyNodeDto>> GetUnitChildrenAsync(Guid companyId, Guid unitId, CancellationToken ct)
    {
        var nodes = new List<HierarchyNodeDto>();

        // Teams in Unit
        var teams = await _unitOfWork.Teams.GetByUnitIdsAsync(new[] { unitId }, ct);
        foreach (var team in teams)
            nodes.Add(await MapTeamNodeAsync(team, unitId, ct));

        // Direct Unit Employees
        var emps = await _unitOfWork.Employees.FindAsync(e => e.UnitId == unitId && e.TeamId == null && !e.IsDeleted, ct);
        foreach (var emp in emps)
            nodes.Add(await MapEmployeeNodeAsync(emp, unitId, null, ct));

        return nodes;
    }

    private async Task<List<HierarchyNodeDto>> GetTeamChildrenAsync(Guid companyId, Guid teamId, CancellationToken ct)
    {
        var emps = await _unitOfWork.Employees.FindAsync(e => e.TeamId == teamId && !e.IsDeleted, ct);
        var nodes = new List<HierarchyNodeDto>();
        foreach (var emp in emps)
            nodes.Add(await MapEmployeeNodeAsync(emp, teamId, null, ct));
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
        var hasChildren = await _unitOfWork.Units.ExistsAsync(u => u.DepartmentId == dept.Id, ct)
            || await _unitOfWork.Employees.ExistsAsync(e => e.DepartmentId == dept.Id && e.UnitId == null && !e.IsDeleted, ct);

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
        var hasChildren = await _unitOfWork.Teams.ExistsAsync(t => t.UnitId == unit.Id, ct)
            || await _unitOfWork.Employees.ExistsAsync(e => e.UnitId == unit.Id && e.TeamId == null && !e.IsDeleted, ct);

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
        var hasChildren = await _unitOfWork.Employees.ExistsAsync(e => e.TeamId == team.Id && !e.IsDeleted, ct);

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
