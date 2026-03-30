using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Identity;
using HrSystemApp.Domain.Models;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.Hierarchy.Queries.GetCompanyHierarchy;

// ─── DTOs ───────────────────────────────────────────────────────────────────

public record TeamHierarchyDto(Guid Id, string Name, string? LeaderName, Guid? LeaderEmployeeId, int MemberCount);
public record UnitHierarchyDto(Guid Id, string Name, string? LeaderName, Guid? LeaderEmployeeId, IReadOnlyList<TeamHierarchyDto> Teams);
public record DepartmentHierarchyDto(Guid Id, string Name, string? VicePresidentName, Guid? VicePresidentId, string? ManagerName, Guid? ManagerId, IReadOnlyList<UnitHierarchyDto> Units);

public record HierarchyPositionDto(UserRole Role, string PositionTitle, int SortOrder);

public record CompanyHierarchyDto(
    Guid CompanyId,
    string CompanyName,
    string? CeoName,
    Guid? CeoEmployeeId,
    IReadOnlyList<HierarchyPositionDto> ConfiguredPositions,
    IReadOnlyList<DepartmentHierarchyDto> Departments);

// ─── Query ───────────────────────────────────────────────────────────────────

public record GetCompanyHierarchyQuery : IRequest<Result<CompanyHierarchyDto>>;

// ─── Handler ─────────────────────────────────────────────────────────────────

public class GetCompanyHierarchyQueryHandler : IRequestHandler<GetCompanyHierarchyQuery, Result<CompanyHierarchyDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<GetCompanyHierarchyQueryHandler> _logger;

    public GetCompanyHierarchyQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        UserManager<ApplicationUser> userManager,
        ILogger<GetCompanyHierarchyQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<Result<CompanyHierarchyDto>> Handle(GetCompanyHierarchyQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<CompanyHierarchyDto>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
            return Result.Failure<CompanyHierarchyDto>(DomainErrors.Employee.NotFound);

        var companyId = employee.CompanyId;

        var company = await _unitOfWork.Companies.GetByIdAsync(companyId, cancellationToken);
        if (company == null)
            return Result.Failure<CompanyHierarchyDto>(DomainErrors.Company.NotFound);

        // Load configured positions
        var positions = await _unitOfWork.HierarchyPositions.GetByCompanyAsync(companyId, cancellationToken);

        // Load departments with full tree (Units → Teams)
        var departments = await _unitOfWork.Departments.GetByCompanyAsync(companyId, cancellationToken);

        // For each department, load units and their teams
        var departmentDtos = new List<DepartmentHierarchyDto>();
        foreach (var dept in departments)
        {
            var units = await _unitOfWork.Units.FindAsync(u => u.DepartmentId == dept.Id && !u.IsDeleted, cancellationToken);
            var unitDtos = new List<UnitHierarchyDto>();

            foreach (var unit in units)
            {
                var teams = await _unitOfWork.Teams.FindAsync(t => t.UnitId == unit.Id && !t.IsDeleted, cancellationToken);
                var teamDtos = teams.Select(t => new TeamHierarchyDto(
                    t.Id,
                    t.Name,
                    t.TeamLeader?.FullName,
                    t.TeamLeaderId,
                    t.Members.Count
                )).ToList();

                string? unitLeaderName = null;
                if (unit.UnitLeaderId.HasValue)
                {
                    var leader = await _unitOfWork.Employees.GetByIdAsync(unit.UnitLeaderId.Value, cancellationToken);
                    unitLeaderName = leader?.FullName;
                }

                unitDtos.Add(new UnitHierarchyDto(unit.Id, unit.Name, unitLeaderName, unit.UnitLeaderId, teamDtos));
            }

            departmentDtos.Add(new DepartmentHierarchyDto(
                dept.Id,
                dept.Name,
                dept.VicePresident?.FullName,
                dept.VicePresidentId,
                dept.Manager?.FullName,
                dept.ManagerId,
                unitDtos));
        }

        // Find CEO: employee in this company with CEO role in Identity
        string? ceoName = null;
        Guid? ceoEmployeeId = null;
        var ceoUsers = await _userManager.GetUsersInRoleAsync(UserRole.CEO.ToString());
        var ceoUserIds = ceoUsers.Select(u => u.Id).ToHashSet();
        var ceoEmployees = await _unitOfWork.Employees.FindAsync(
            e => e.CompanyId == companyId && !e.IsDeleted && e.UserId != null && ceoUserIds.Contains(e.UserId!),
            cancellationToken);
        var ceo = ceoEmployees.FirstOrDefault();
        if (ceo != null)
        {
            ceoName = ceo.FullName;
            ceoEmployeeId = ceo.Id;
        }

        _logger.LogInformation("Hierarchy retrieved for company {CompanyId}. Departments: {Dept}, Positions: {Pos}",
            companyId, departmentDtos.Count, positions.Count);

        var dto = new CompanyHierarchyDto(
            companyId,
            company.CompanyName,
            ceoName,
            ceoEmployeeId,
            positions.Select(p => new HierarchyPositionDto(p.Role, p.PositionTitle, p.SortOrder)).ToList(),
            departmentDtos);

        return Result.Success(dto);
    }
}
