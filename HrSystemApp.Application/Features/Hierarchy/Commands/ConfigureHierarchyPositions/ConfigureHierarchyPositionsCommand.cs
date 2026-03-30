using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.Hierarchy.Commands.ConfigureHierarchyPositions;

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record HierarchyPositionInputDto(UserRole Role, string PositionTitle, int SortOrder);

// ─── Command ─────────────────────────────────────────────────────────────────

public record ConfigureHierarchyPositionsCommand(List<HierarchyPositionInputDto> Positions)
    : IRequest<Result<int>>;

// ─── Handler ─────────────────────────────────────────────────────────────────

public class ConfigureHierarchyPositionsCommandHandler
    : IRequestHandler<ConfigureHierarchyPositionsCommand, Result<int>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ConfigureHierarchyPositionsCommandHandler> _logger;

    // Roles that are part of the hierarchy chain (excludes support roles)
    private static readonly HashSet<UserRole> AllowedHierarchyRoles = new()
    {
        UserRole.CEO,
        UserRole.VicePresident,
        UserRole.DepartmentManager,
        UserRole.UnitLeader,
        UserRole.TeamLeader,
        UserRole.HR,
        UserRole.AssetAdmin,
        UserRole.CompanyAdmin,
    };

    public ConfigureHierarchyPositionsCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<ConfigureHierarchyPositionsCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<int>> Handle(ConfigureHierarchyPositionsCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<int>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
            return Result.Failure<int>(DomainErrors.Employee.NotFound);

        var companyId = employee.CompanyId;

        // Validate: no SuperAdmin allowed
        var invalidRoles = request.Positions
            .Where(p => p.Role == UserRole.SuperAdmin || !AllowedHierarchyRoles.Contains(p.Role))
            .Select(p => p.Role)
            .ToList();

        if (invalidRoles.Any())
        {
            _logger.LogWarning("ConfigureHierarchy failed: Invalid roles provided: {Roles}", string.Join(", ", invalidRoles));
            return Result.Failure<int>(DomainErrors.Hierarchy.InvalidRole);
        }

        // Validate: no duplicate roles
        var duplicates = request.Positions
            .GroupBy(p => p.Role)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Any())
        {
            _logger.LogWarning("ConfigureHierarchy failed: Duplicate roles in payload: {Roles}", string.Join(", ", duplicates));
            return Result.Failure<int>(DomainErrors.Hierarchy.DuplicateRole);
        }

        // Validate: only one CEO
        if (request.Positions.Count(p => p.Role == UserRole.CEO) > 1)
            return Result.Failure<int>(DomainErrors.Hierarchy.MultipleCeos);

        // Replace existing positions (idempotent)
        await _unitOfWork.HierarchyPositions.DeleteAllForCompanyAsync(companyId, cancellationToken);

        var newPositions = request.Positions.Select(p => new CompanyHierarchyPosition
        {
            CompanyId = companyId,
            Role = p.Role,
            PositionTitle = p.PositionTitle,
            SortOrder = p.SortOrder
        }).ToList();

        foreach (var position in newPositions)
            await _unitOfWork.HierarchyPositions.AddAsync(position, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Hierarchy configured for company {CompanyId} with {Count} positions.", companyId, newPositions.Count);

        return Result.Success(newPositions.Count);
    }
}
