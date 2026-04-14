using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.OrgNodes;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.OrgNodes.Queries.GetUnlinkedEntities;

public record GetUnlinkedEntitiesQuery : IRequest<Result<UnlinkedEntitiesResponse>>;

public class GetUnlinkedEntitiesQueryHandler : IRequestHandler<GetUnlinkedEntitiesQuery, Result<UnlinkedEntitiesResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetUnlinkedEntitiesQueryHandler> _logger;

    public GetUnlinkedEntitiesQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<GetUnlinkedEntitiesQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<UnlinkedEntitiesResponse>> Handle(GetUnlinkedEntitiesQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting unlinked entities (D/U/T not in any OrgNode)");

        // Get all linked entity IDs for each type
        var allNodes = await _unitOfWork.OrgNodes.GetAllAsync(cancellationToken);

        var linkedDepartmentIds = allNodes
            .Where(n => n.EntityType == OrgEntityType.Department && n.EntityId.HasValue)
            .Select(n => n.EntityId!.Value)
            .ToHashSet();

        var linkedUnitIds = allNodes
            .Where(n => n.EntityType == OrgEntityType.Unit && n.EntityId.HasValue)
            .Select(n => n.EntityId!.Value)
            .ToHashSet();

        var linkedTeamIds = allNodes
            .Where(n => n.EntityType == OrgEntityType.Team && n.EntityId.HasValue)
            .Select(n => n.EntityId!.Value)
            .ToHashSet();

        // Get user info
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<UnlinkedEntitiesResponse>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
            return Result.Failure<UnlinkedEntitiesResponse>(DomainErrors.Employee.NotFound);

        // Get all departments/units/teams for the company
        var allDepartments = await _unitOfWork.Departments.GetByCompanyAsync(employee.CompanyId, cancellationToken);
        var unlinkedDepartments = allDepartments
            .Where(d => !linkedDepartmentIds.Contains(d.Id))
            .Select(d => new UnlinkedDepartmentDto(d.Id, d.Name))
            .ToList();

        var allUnits = new List<HrSystemApp.Domain.Models.Unit>();
        foreach (var dept in allDepartments)
        {
            var units = await _unitOfWork.Units.GetByDepartmentAsync(dept.Id, cancellationToken);
            allUnits.AddRange(units);
        }
        var unlinkedUnits = allUnits
            .Where(u => !linkedUnitIds.Contains(u.Id))
            .Select(u => new UnlinkedUnitDto(u.Id, u.Name, allDepartments.First(d => d.Id == u.DepartmentId).Name))
            .ToList();

        var allTeams = new List<HrSystemApp.Domain.Models.Team>();
        foreach (var unit in allUnits)
        {
            var teams = await _unitOfWork.Teams.GetByUnitAsync(unit.Id, cancellationToken);
            allTeams.AddRange(teams);
        }
        var unlinkedTeams = allTeams
            .Where(t => !linkedTeamIds.Contains(t.Id))
            .Select(t => new UnlinkedTeamDto(t.Id, t.Name, allUnits.First(u => u.Id == t.UnitId).Name))
            .ToList();

        return Result.Success(new UnlinkedEntitiesResponse(unlinkedDepartments, unlinkedUnits, unlinkedTeams));
    }
}