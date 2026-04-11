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
    private readonly IHierarchyService _hierarchyService;
    private readonly ILogger<GetCompanyHierarchyQueryHandler> _logger;

    public GetCompanyHierarchyQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IHierarchyService hierarchyService,
        ILogger<GetCompanyHierarchyQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _hierarchyService = hierarchyService;
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

        {
            // Initial Load: Discover Roots
            nodes.AddRange(await GetRootsAsync(companyId, cancellationToken));
        }
        else
        {
            // Expansion Load: Discover Children via Zig-Zag Pattern
            var children = await _hierarchyService.GetHierarchyChildrenAsync(companyId, request.ParentId.Value, request.ParentType ?? "Employee", cancellationToken);
            if (children.Any())
            {
                var metadata = await _hierarchyService.GetNodesMetadataAsync(children, cancellationToken);
                foreach (var child in children)
                {
                    if (metadata.TryGetValue(child.Id, out var data))
                    {
                        nodes.Add(new HierarchyNodeDto(
                            child.Id,
                            request.ParentId,
                            child.Type == "Employee" ? data.FullName : data.Name,
                            child.Type,
                            child.Type == "Employee" ? data.Role : null,
                            child.Type == "Employee" ? data.Role : null, // Default position to role for now
                            data.HasChildren,
                            new HierarchyMetadata(
                                child.Type == "Employee" ? data.Email : null,
                                child.Type == "Employee" ? data.EmployeeCode : null,
                                null,
                                child.Type == "Employee" ? data.ManagerName : data.LeaderName,
                                child.Type == "Employee" ? data.ManagerId : null
                            )));
                    }
                }
            }
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

        return nodes;
    }
}
