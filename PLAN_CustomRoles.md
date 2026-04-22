# Implementation Plan: Custom Company Roles + Role-Based Approval Steps

## Overview

This plan introduces two tightly related features:
1. **Custom per-company roles** — HR admins can create named roles (e.g. "Finance Approver", "Legal Reviewer"), assign feature-level permissions to them, and assign employees to them. Roles support soft delete.
2. **`CompanyRole` workflow step type** — A new workflow step type where _any active employee holding the specified role_ can approve the request. "First one wins" — the moment one approves, the step advances and the request disappears from all other role holders. This type can be freely mixed with the existing `OrgNode`, `DirectEmployee`, and `HierarchyLevel` step types.

## Codebase Conventions (read before writing any code)

- **Entities**: extend `BaseEntity` (has `Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted`) or `AuditableEntity` (extends `BaseEntity`, adds `CreatedById`, `UpdatedById`). Business entities use `AuditableEntity`; junction/detail tables use `BaseEntity`.
- **Soft delete is automatic**: `ApplicationDbContext.HandleEntityStateChanges` intercepts `EntityState.Deleted` and sets `IsDeleted = true` for all `BaseEntity` subclasses unless they implement `IHardDelete`. Global query filters also applied automatically via `ApplySoftDeleteQueryFilter`. Do NOT add manual `IsDeleted` checks or custom query filters for soft-deleting entities.
- **Hard delete**: Implement `IHardDelete` (from `HrSystemApp.Domain.Common`) on an entity to make EF actually delete the row.
- **EF configs**: Create an `IEntityTypeConfiguration<T>` class in `HrSystemApp.Infrastructure/Data/Configurations/`. They are auto-discovered via `builder.ApplyConfigurationsFromAssembly(...)`. Add `DbSet<T>` to `ApplicationDbContext`.
- **Repositories**: Interface in `Application/Interfaces/Repositories/`, implementation in `Infrastructure/Repositories/`, extending `Repository<T>` base. Wire in `IUnitOfWork` / `UnitOfWork`.
- **Commands/Queries**: MediatR. File naming: `{Name}Command.cs` contains both the record and the handler class. Same for queries.
- **Results**: Use `Result<T>` / `Result.Success(...)` / `Result.Failure<T>(DomainErrors.X.Y)`.
- **Errors**: Add to `DomainErrors.cs`. Use plain string literals (not `Messages.Errors.*`) for new error messages in this feature — the pattern `new Error("Code", "Message")` is fine.
- **Controllers**: Inherit `BaseApiController`, use `HandleResult(result)`. JwtBearer auth via `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`.
- **Authorization roles**: Use string constants from `HrSystemApp.Api.Authorization.Roles`.
- **Namespace pattern**: `HrSystemApp.{Layer}.{Feature}`.

---

## Step 1 — Domain Models (3 new files, 3 modified files)

### 1a. NEW: `HrSystemApp.Domain/Models/CompanyRole.cs`

```csharp
using HrSystemApp.Domain.Common;

namespace HrSystemApp.Domain.Models;

/// <summary>
/// A custom, named role scoped to a company. Employees can be assigned to it,
/// and workflow approval steps can require it.
/// Soft-deleted automatically by BaseEntity conventions — do not hard-delete.
/// </summary>
public class CompanyRole : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
    public ICollection<CompanyRolePermission> Permissions { get; set; } = new List<CompanyRolePermission>();
    public ICollection<EmployeeCompanyRole> EmployeeRoles { get; set; } = new List<EmployeeCompanyRole>();
}
```

### 1b. NEW: `HrSystemApp.Domain/Models/CompanyRolePermission.cs`

Implements `IHardDelete` so permissions can be cleanly replaced when a role is updated (old ones are hard-deleted, new ones are inserted).

```csharp
using HrSystemApp.Domain.Common;

namespace HrSystemApp.Domain.Models;

/// <summary>
/// A single feature-level permission granted by a CompanyRole.
/// Hard-deleted (not soft-deleted) because replacing a role's permissions
/// means deleting the old set and inserting the new set.
/// </summary>
public class CompanyRolePermission : BaseEntity, IHardDelete
{
    public Guid RoleId { get; set; }

    /// <summary>
    /// One of the string constants defined in AppPermissions.
    /// </summary>
    public string Permission { get; set; } = string.Empty;

    // Navigation
    public CompanyRole Role { get; set; } = null!;
}
```

### 1c. NEW: `HrSystemApp.Domain/Models/EmployeeCompanyRole.cs`

Implements `IHardDelete` so that removing a role assignment deletes the row rather than soft-deleting.

```csharp
using HrSystemApp.Domain.Common;

namespace HrSystemApp.Domain.Models;

/// <summary>
/// Junction table: assigns a CompanyRole to an Employee (many-to-many).
/// Hard-deleted on removal because "unassignment" means the row should disappear.
/// </summary>
public class EmployeeCompanyRole : BaseEntity, IHardDelete
{
    public Guid EmployeeId { get; set; }
    public Guid RoleId { get; set; }
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public Employee Employee { get; set; } = null!;
    public CompanyRole Role { get; set; } = null!;
}
```

### 1d. MODIFY: `HrSystemApp.Domain/Enums/WorkflowStepType.cs`

Add `CompanyRole = 3`:

```csharp
namespace HrSystemApp.Domain.Enums;

public enum WorkflowStepType
{
    OrgNode = 0,
    DirectEmployee = 1,
    HierarchyLevel = 2,
    CompanyRole = 3
}
```

### 1e. MODIFY: `HrSystemApp.Domain/Models/RequestWorkflow.cs`

In the `RequestWorkflowStep` class, add two new properties after the existing `DirectEmployeeId` property:

```csharp
/// <summary>
/// The custom company role whose holders can approve this step.
/// Any one of them approving advances the request (first-wins).
/// Only set when StepType is CompanyRole.
/// </summary>
public Guid? CompanyRoleId { get; set; }
public CompanyRole? CompanyRole { get; set; }
```

### 1f. MODIFY: `HrSystemApp.Domain/Models/Employee.cs`

Add reverse navigation after the existing `ICollection<AttendanceLog>` property:

```csharp
public ICollection<EmployeeCompanyRole> CompanyRoles { get; set; } = new List<EmployeeCompanyRole>();
```

### 1g. NEW: `HrSystemApp.Domain/Constants/AppPermissions.cs`

```csharp
namespace HrSystemApp.Domain.Constants;

/// <summary>
/// All available feature-level permission strings.
/// Each string matches a policy name registered in DI.
/// </summary>
public static class AppPermissions
{
    public const string ViewAllAttendance  = "attendance.view_all";
    public const string OverrideAttendance = "attendance.override";
    public const string ViewAllRequests    = "requests.view_all";
    public const string ManageEmployees    = "employees.manage";
    public const string ViewReports        = "reports.view";

    public static readonly IReadOnlyList<string> All = new[]
    {
        ViewAllAttendance,
        OverrideAttendance,
        ViewAllRequests,
        ManageEmployees,
        ViewReports
    };
}
```

---

## Step 2 — EF Configuration & DbContext (3 new config files, 2 modifications)

### 2a. NEW: `HrSystemApp.Infrastructure/Data/Configurations/CompanyRoleConfiguration.cs`

```csharp
using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class CompanyRoleConfiguration : IEntityTypeConfiguration<CompanyRole>
{
    public void Configure(EntityTypeBuilder<CompanyRole> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        // Two roles in the same company cannot share a name (among non-deleted ones).
        // The soft-delete global filter means deleted roles are invisible, so this
        // unique index applies only to live rows.
        builder.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique();

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Permissions)
            .WithOne(p => p.Role)
            .HasForeignKey(p => p.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.EmployeeRoles)
            .WithOne(er => er.Role)
            .HasForeignKey(er => er.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### 2b. NEW: `HrSystemApp.Infrastructure/Data/Configurations/CompanyRolePermissionConfiguration.cs`

```csharp
using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class CompanyRolePermissionConfiguration : IEntityTypeConfiguration<CompanyRolePermission>
{
    public void Configure(EntityTypeBuilder<CompanyRolePermission> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Permission)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => new { x.RoleId, x.Permission }).IsUnique();
    }
}
```

### 2c. NEW: `HrSystemApp.Infrastructure/Data/Configurations/EmployeeCompanyRoleConfiguration.cs`

```csharp
using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class EmployeeCompanyRoleConfiguration : IEntityTypeConfiguration<EmployeeCompanyRole>
{
    public void Configure(EntityTypeBuilder<EmployeeCompanyRole> builder)
    {
        builder.HasKey(x => x.Id);

        // Prevent assigning the same role to the same employee twice
        builder.HasIndex(x => new { x.EmployeeId, x.RoleId }).IsUnique();

        builder.HasOne(x => x.Employee)
            .WithMany(e => e.CompanyRoles)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Role → EmployeeCompanyRole cascade is configured in CompanyRoleConfiguration
    }
}
```

### 2d. MODIFY: `HrSystemApp.Infrastructure/Data/Configurations/RequestWorkflowStepConfiguration.cs` (or wherever the step is configured)

Find the existing configuration for `RequestWorkflowStep` and add:

```csharp
builder.Property(s => s.CompanyRoleId).IsRequired(false);

builder.HasOne(s => s.CompanyRole)
    .WithMany()
    .HasForeignKey(s => s.CompanyRoleId)
    .OnDelete(DeleteBehavior.Restrict)
    .IsRequired(false);
```

### 2e. MODIFY: `HrSystemApp.Infrastructure/Data/ApplicationDbContext.cs`

Add three new `DbSet` properties after the existing ones:

```csharp
public DbSet<CompanyRole> CompanyRoles { get; set; } = null!;
public DbSet<CompanyRolePermission> CompanyRolePermissions { get; set; } = null!;
public DbSet<EmployeeCompanyRole> EmployeeCompanyRoles { get; set; } = null!;
```

### 2f. Run Migration

After all code changes in Steps 1–2 are done, run:

```
dotnet ef migrations add AddCompanyRoles --project HrSystemApp.Infrastructure --startup-project HrSystemApp.Api
```

This will generate a migration that creates:
- `CompanyRoles` table
- `CompanyRolePermissions` table
- `EmployeeCompanyRoles` table
- Adds nullable `CompanyRoleId` column to `RequestWorkflowSteps`

---

## Step 3 — Repository Interfaces & Implementations

### 3a. NEW: `HrSystemApp.Application/Interfaces/Repositories/ICompanyRoleRepository.cs`

```csharp
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface ICompanyRoleRepository : IRepository<CompanyRole>
{
    /// <summary>
    /// Gets a role with its Permissions collection loaded. Returns null if not found or soft-deleted.
    /// </summary>
    Task<CompanyRole?> GetWithPermissionsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns all non-deleted roles for a company, ordered by Name.
    /// </summary>
    Task<IReadOnlyList<CompanyRole>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>
    /// Returns true if a non-deleted role with the given name already exists in this company.
    /// Pass excludeId to ignore a specific role (used during updates).
    /// </summary>
    Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? excludeId, CancellationToken ct = default);
}
```

### 3b. NEW: `HrSystemApp.Application/Interfaces/Repositories/IEmployeeCompanyRoleRepository.cs`

```csharp
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IEmployeeCompanyRoleRepository : IRepository<EmployeeCompanyRole>
{
    /// <summary>
    /// Returns all role assignments for an employee (with Role navigation loaded).
    /// </summary>
    Task<IReadOnlyList<EmployeeCompanyRole>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default);

    /// <summary>
    /// Returns all active employees assigned to the given role.
    /// Only returns employees with EmploymentStatus == Active.
    /// </summary>
    Task<IReadOnlyList<Employee>> GetActiveEmployeesByRoleIdAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the employee already has this role assigned.
    /// </summary>
    Task<bool> ExistsAsync(Guid employeeId, Guid roleId, CancellationToken ct = default);

    /// <summary>
    /// Hard-removes the assignment. No-op if not found (does not throw).
    /// </summary>
    Task RemoveAsync(Guid employeeId, Guid roleId, CancellationToken ct = default);

    /// <summary>
    /// Returns the distinct permission strings granted to an employee across all their assigned roles.
    /// Used for authorization checks.
    /// </summary>
    Task<IReadOnlyList<string>> GetPermissionsForEmployeeAsync(Guid employeeId, CancellationToken ct = default);
}
```

### 3c. NEW: `HrSystemApp.Infrastructure/Repositories/CompanyRoleRepository.cs`

```csharp
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class CompanyRoleRepository : Repository<CompanyRole>, ICompanyRoleRepository
{
    public CompanyRoleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<CompanyRole?> GetWithPermissionsAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<IReadOnlyList<CompanyRole>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(r => r.Permissions)
            .Where(r => r.CompanyId == companyId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? excludeId, CancellationToken ct = default)
    {
        var query = _dbSet.Where(r => r.CompanyId == companyId
                                   && r.Name.ToLower() == name.ToLower());
        if (excludeId.HasValue)
            query = query.Where(r => r.Id != excludeId.Value);

        return await query.AnyAsync(ct);
    }
}
```

### 3d. NEW: `HrSystemApp.Infrastructure/Repositories/EmployeeCompanyRoleRepository.cs`

```csharp
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class EmployeeCompanyRoleRepository : Repository<EmployeeCompanyRole>, IEmployeeCompanyRoleRepository
{
    public EmployeeCompanyRoleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<EmployeeCompanyRole>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(er => er.Role)
                .ThenInclude(r => r.Permissions)
            .Where(er => er.EmployeeId == employeeId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Employee>> GetActiveEmployeesByRoleIdAsync(Guid roleId, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(er => er.Employee)
            .Where(er => er.RoleId == roleId
                      && er.Employee.EmploymentStatus == EmploymentStatus.Active)
            .Select(er => er.Employee)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(Guid employeeId, Guid roleId, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(er => er.EmployeeId == employeeId && er.RoleId == roleId, ct);
    }

    public async Task RemoveAsync(Guid employeeId, Guid roleId, CancellationToken ct = default)
    {
        // EmployeeCompanyRole implements IHardDelete so _dbSet.Remove causes a real DELETE
        var assignment = await _dbSet
            .FirstOrDefaultAsync(er => er.EmployeeId == employeeId && er.RoleId == roleId, ct);
        if (assignment is not null)
            _dbSet.Remove(assignment);
    }

    public async Task<IReadOnlyList<string>> GetPermissionsForEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(er => er.EmployeeId == employeeId)
            .SelectMany(er => er.Role.Permissions.Select(p => p.Permission))
            .Distinct()
            .ToListAsync(ct);
    }
}
```

### 3e. MODIFY: `HrSystemApp.Application/Interfaces/IUnitOfWork.cs`

Add two new properties (anywhere in the interface alongside the existing repo properties):

```csharp
ICompanyRoleRepository CompanyRoles { get; }
IEmployeeCompanyRoleRepository EmployeeCompanyRoles { get; }
```

### 3f. MODIFY: `HrSystemApp.Infrastructure/Repositories/UnitOfWork.cs`

Add two private fields at the top alongside the existing ones:

```csharp
private ICompanyRoleRepository? _companyRoleRepository;
private IEmployeeCompanyRoleRepository? _employeeCompanyRoleRepository;
```

Add two public properties:

```csharp
public ICompanyRoleRepository CompanyRoles =>
    _companyRoleRepository ??= new CompanyRoleRepository(_context);

public IEmployeeCompanyRoleRepository EmployeeCompanyRoles =>
    _employeeCompanyRoleRepository ??= new EmployeeCompanyRoleRepository(_context);
```

---

## Step 4 — Application DTOs

### 4a. NEW: `HrSystemApp.Application/DTOs/Roles/CompanyRoleDto.cs`

```csharp
namespace HrSystemApp.Application.DTOs.Roles;

public sealed record CompanyRoleSummaryDto(
    Guid Id,
    string Name,
    string? Description);

public sealed record CompanyRoleDto(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions);
```

### 4b. MODIFY: `HrSystemApp.Application/DTOs/Requests/WorkflowStepDto.cs`

The file is truncated in the codebase — rewrite it in full. The existing fields must be preserved exactly. Add `CompanyRoleId` at the end:

```csharp
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.Requests;

public class WorkflowStepDto
{
    public WorkflowStepType StepType { get; set; } = WorkflowStepType.OrgNode;

    /// <summary>
    /// Human-readable name of the step type: "OrgNode", "DirectEmployee", "HierarchyLevel", or "CompanyRole".
    /// </summary>
    public string StepTypeName { get; set; } = string.Empty;

    public Guid? OrgNodeId { get; set; }
    public bool BypassHierarchyCheck { get; set; } = false;
    public Guid? DirectEmployeeId { get; set; }

    /// <summary>
    /// For HierarchyLevel steps: first ancestor level to include (1-based).
    /// Defaults to 1 when omitted. Null on other step types.
    /// </summary>
    public int? StartFromLevel { get; set; }

    /// <summary>
    /// For HierarchyLevel steps: how many consecutive ancestor levels to include.
    /// Must be >= 1. Null on other step types.
    /// </summary>
    public int? LevelsUp { get; set; }

    public int SortOrder { get; set; }

    /// <summary>
    /// For CompanyRole steps: the ID of the custom company role whose holders can approve.
    /// Null on other step types.
    /// </summary>
    public Guid? CompanyRoleId { get; set; }
}
```

### 4c. MODIFY: `HrSystemApp.Application/DTOs/Requests/PlannedStepDto.cs`

The file may be truncated. Rewrite it in full, adding `CompanyRoleId` and `RoleName`:

```csharp
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.Requests;

/// <summary>
/// A snapshotted step in the approval chain, stored as JSON on the Request at submission time.
/// Immutable after creation — changes to org structure do not retroactively affect in-flight requests.
/// </summary>
public class PlannedStepDto
{
    public WorkflowStepType StepType { get; set; }

    /// <summary>For OrgNode/HierarchyLevel steps: the OrgNode ID.</summary>
    public Guid? NodeId { get; set; }

    /// <summary>Display name for the step (OrgNode name, employee name, or role name).</summary>
    public string NodeName { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>For HierarchyLevel steps: the ancestor level this was resolved from.</summary>
    public int? ResolvedFromLevel { get; set; }

    /// <summary>For CompanyRole steps: the role ID.</summary>
    public Guid? CompanyRoleId { get; set; }

    /// <summary>For CompanyRole steps: the role name (display).</summary>
    public string? RoleName { get; set; }

    public List<ApproverDto> Approvers { get; set; } = new();
}

public class ApproverDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
}
```

---

## Step 5 — Commands & Queries for Role Management

### 5a. NEW: `HrSystemApp.Application/Features/Roles/Commands/CreateCompanyRole/CreateCompanyRoleCommand.cs`

```csharp
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Constants;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Commands.CreateCompanyRole;

public sealed record CreateCompanyRoleCommand(
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions) : IRequest<Result<Guid>>;

public class CreateCompanyRoleCommandHandler : IRequestHandler<CreateCompanyRoleCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public CreateCompanyRoleCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Guid>> Handle(CreateCompanyRoleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee is null)
            return Result.Failure<Guid>(DomainErrors.Employee.NotFound);

        // Validate permissions are all known values
        var invalidPermissions = request.Permissions
            .Where(p => !AppPermissions.All.Contains(p))
            .ToList();
        if (invalidPermissions.Any())
            return Result.Failure<Guid>(DomainErrors.Roles.InvalidPermission);

        // Name uniqueness within company
        if (await _unitOfWork.CompanyRoles.ExistsByNameAsync(employee.CompanyId, request.Name, null, cancellationToken))
            return Result.Failure<Guid>(DomainErrors.Roles.NameAlreadyExists);

        var role = new CompanyRole
        {
            CompanyId = employee.CompanyId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Permissions = request.Permissions
                .Distinct()
                .Select(p => new CompanyRolePermission { Permission = p })
                .ToList()
        };

        await _unitOfWork.CompanyRoles.AddAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(role.Id);
    }
}
```

### 5b. NEW: `HrSystemApp.Application/Features/Roles/Commands/UpdateCompanyRole/UpdateCompanyRoleCommand.cs`

```csharp
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Constants;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Commands.UpdateCompanyRole;

public sealed record UpdateCompanyRoleCommand(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions) : IRequest<Result<bool>>;

public class UpdateCompanyRoleCommandHandler : IRequestHandler<UpdateCompanyRoleCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateCompanyRoleCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(UpdateCompanyRoleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee is null)
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);

        var role = await _unitOfWork.CompanyRoles.GetWithPermissionsAsync(request.Id, cancellationToken);
        if (role is null || role.CompanyId != employee.CompanyId)
            return Result.Failure<bool>(DomainErrors.Roles.NotFound);

        // Validate permissions
        var invalidPermissions = request.Permissions
            .Where(p => !AppPermissions.All.Contains(p))
            .ToList();
        if (invalidPermissions.Any())
            return Result.Failure<bool>(DomainErrors.Roles.InvalidPermission);

        // Name uniqueness (excluding self)
        if (await _unitOfWork.CompanyRoles.ExistsByNameAsync(employee.CompanyId, request.Name, request.Id, cancellationToken))
            return Result.Failure<bool>(DomainErrors.Roles.NameAlreadyExists);

        role.Name = request.Name.Trim();
        role.Description = request.Description?.Trim();

        // Replace permissions: remove old ones (hard-delete via IHardDelete) and add new ones
        foreach (var perm in role.Permissions.ToList())
            _unitOfWork.Context.Set<CompanyRolePermission>().Remove(perm);  // hard delete

        role.Permissions = request.Permissions
            .Distinct()
            .Select(p => new CompanyRolePermission { RoleId = role.Id, Permission = p })
            .ToList();

        await _unitOfWork.CompanyRoles.UpdateAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
```

> **Note on `_unitOfWork.Context`**: The `UpdateCompanyRoleCommand` handler needs to call `_dbSet.Remove(perm)` to hard-delete `CompanyRolePermission` records. The cleanest way is to expose `ApplicationDbContext` on `IUnitOfWork` or add a `DeletePermissionsForRoleAsync` method to `ICompanyRoleRepository`. See the implementation note in Step 5g.

### 5c. NEW: `HrSystemApp.Application/Features/Roles/Commands/DeleteCompanyRole/DeleteCompanyRoleCommand.cs`

```csharp
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Commands.DeleteCompanyRole;

public sealed record DeleteCompanyRoleCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteCompanyRoleCommandHandler : IRequestHandler<DeleteCompanyRoleCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public DeleteCompanyRoleCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(DeleteCompanyRoleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee is null)
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);

        var role = await _unitOfWork.CompanyRoles.GetByIdAsync(request.Id, cancellationToken);
        if (role is null || role.CompanyId != employee.CompanyId)
            return Result.Failure<bool>(DomainErrors.Roles.NotFound);

        // Guard: cannot delete a role that is still referenced by an active workflow definition
        var isInUse = await _unitOfWork.RequestDefinitions
            .IsRoleInUseAsync(request.Id, cancellationToken);
        if (isInUse)
            return Result.Failure<bool>(DomainErrors.Roles.InUseByWorkflow);

        // Soft-delete: BaseEntity conventions handle this via EntityState.Deleted interception
        await _unitOfWork.CompanyRoles.DeleteAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
```

### 5d. NEW: `HrSystemApp.Application/Features/Roles/Commands/AssignRoleToEmployee/AssignRoleToEmployeeCommand.cs`

```csharp
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Commands.AssignRoleToEmployee;

public sealed record AssignRoleToEmployeeCommand(
    Guid EmployeeId,
    Guid RoleId) : IRequest<Result<bool>>;

public class AssignRoleToEmployeeCommandHandler : IRequestHandler<AssignRoleToEmployeeCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AssignRoleToEmployeeCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(AssignRoleToEmployeeCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);

        var currentEmployee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (currentEmployee is null)
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);

        // Load the target employee and verify same company
        var targetEmployee = await _unitOfWork.Employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (targetEmployee is null || targetEmployee.CompanyId != currentEmployee.CompanyId)
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);

        // Load and verify the role belongs to the same company
        var role = await _unitOfWork.CompanyRoles.GetByIdAsync(request.RoleId, cancellationToken);
        if (role is null || role.CompanyId != currentEmployee.CompanyId)
            return Result.Failure<bool>(DomainErrors.Roles.NotFound);

        // Check not already assigned
        if (await _unitOfWork.EmployeeCompanyRoles.ExistsAsync(request.EmployeeId, request.RoleId, cancellationToken))
            return Result.Failure<bool>(DomainErrors.Roles.AlreadyAssigned);

        var assignment = new EmployeeCompanyRole
        {
            EmployeeId = request.EmployeeId,
            RoleId = request.RoleId,
            AssignedAtUtc = DateTime.UtcNow
        };

        await _unitOfWork.EmployeeCompanyRoles.AddAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
```

### 5e. NEW: `HrSystemApp.Application/Features/Roles/Commands/RemoveRoleFromEmployee/RemoveRoleFromEmployeeCommand.cs`

```csharp
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Commands.RemoveRoleFromEmployee;

public sealed record RemoveRoleFromEmployeeCommand(
    Guid EmployeeId,
    Guid RoleId) : IRequest<Result<bool>>;

public class RemoveRoleFromEmployeeCommandHandler : IRequestHandler<RemoveRoleFromEmployeeCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public RemoveRoleFromEmployeeCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(RemoveRoleFromEmployeeCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<bool>(DomainErrors.Auth.Unauthorized);

        var currentEmployee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (currentEmployee is null)
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);

        // Verify target employee is in the same company
        var targetEmployee = await _unitOfWork.Employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (targetEmployee is null || targetEmployee.CompanyId != currentEmployee.CompanyId)
            return Result.Failure<bool>(DomainErrors.Employee.NotFound);

        if (!await _unitOfWork.EmployeeCompanyRoles.ExistsAsync(request.EmployeeId, request.RoleId, cancellationToken))
            return Result.Failure<bool>(DomainErrors.Roles.AssignmentNotFound);

        await _unitOfWork.EmployeeCompanyRoles.RemoveAsync(request.EmployeeId, request.RoleId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
```

### 5f. NEW Queries

**`HrSystemApp.Application/Features/Roles/Queries/GetCompanyRoles/GetCompanyRolesQuery.cs`**

```csharp
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Roles;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Queries.GetCompanyRoles;

public sealed record GetCompanyRolesQuery : IRequest<Result<IReadOnlyList<CompanyRoleDto>>>;

public class GetCompanyRolesQueryHandler : IRequestHandler<GetCompanyRolesQuery, Result<IReadOnlyList<CompanyRoleDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetCompanyRolesQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<CompanyRoleDto>>> Handle(
        GetCompanyRolesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<IReadOnlyList<CompanyRoleDto>>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee is null)
            return Result.Failure<IReadOnlyList<CompanyRoleDto>>(DomainErrors.Employee.NotFound);

        var roles = await _unitOfWork.CompanyRoles.GetByCompanyAsync(employee.CompanyId, cancellationToken);

        var dtos = roles.Select(r => new CompanyRoleDto(
            r.Id,
            r.Name,
            r.Description,
            r.Permissions.Select(p => p.Permission).ToList()
        )).ToList();

        return Result.Success<IReadOnlyList<CompanyRoleDto>>(dtos);
    }
}
```

**`HrSystemApp.Application/Features/Roles/Queries/GetCompanyRoleById/GetCompanyRoleByIdQuery.cs`**

```csharp
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Roles;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Queries.GetCompanyRoleById;

public sealed record GetCompanyRoleByIdQuery(Guid Id) : IRequest<Result<CompanyRoleDto>>;

public class GetCompanyRoleByIdQueryHandler : IRequestHandler<GetCompanyRoleByIdQuery, Result<CompanyRoleDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetCompanyRoleByIdQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<CompanyRoleDto>> Handle(
        GetCompanyRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<CompanyRoleDto>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee is null)
            return Result.Failure<CompanyRoleDto>(DomainErrors.Employee.NotFound);

        var role = await _unitOfWork.CompanyRoles.GetWithPermissionsAsync(request.Id, cancellationToken);
        if (role is null || role.CompanyId != employee.CompanyId)
            return Result.Failure<CompanyRoleDto>(DomainErrors.Roles.NotFound);

        return Result.Success(new CompanyRoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.Permissions.Select(p => p.Permission).ToList()
        ));
    }
}
```

**`HrSystemApp.Application/Features/Roles/Queries/GetEmployeeRoles/GetEmployeeRolesQuery.cs`**

```csharp
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Roles;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Queries.GetEmployeeRoles;

public sealed record GetEmployeeRolesQuery(Guid EmployeeId) : IRequest<Result<IReadOnlyList<CompanyRoleSummaryDto>>>;

public class GetEmployeeRolesQueryHandler : IRequestHandler<GetEmployeeRolesQuery, Result<IReadOnlyList<CompanyRoleSummaryDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetEmployeeRolesQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<CompanyRoleSummaryDto>>> Handle(
        GetEmployeeRolesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<IReadOnlyList<CompanyRoleSummaryDto>>(DomainErrors.Auth.Unauthorized);

        var currentEmployee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (currentEmployee is null)
            return Result.Failure<IReadOnlyList<CompanyRoleSummaryDto>>(DomainErrors.Employee.NotFound);

        // Verify target employee is in same company
        var targetEmployee = await _unitOfWork.Employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (targetEmployee is null || targetEmployee.CompanyId != currentEmployee.CompanyId)
            return Result.Failure<IReadOnlyList<CompanyRoleSummaryDto>>(DomainErrors.Employee.NotFound);

        var assignments = await _unitOfWork.EmployeeCompanyRoles.GetByEmployeeIdAsync(request.EmployeeId, cancellationToken);

        var dtos = assignments.Select(er => new CompanyRoleSummaryDto(
            er.Role.Id,
            er.Role.Name,
            er.Role.Description
        )).ToList();

        return Result.Success<IReadOnlyList<CompanyRoleSummaryDto>>(dtos);
    }
}
```

### 5g. Implementation Note: Replacing Permissions on Update

In `UpdateCompanyRoleCommandHandler`, replacing the role's permissions requires hard-deleting `CompanyRolePermission` rows. Because `IHardDelete` is on the entity, calling EF's `Remove()` skips the soft-delete intercept.

The cleanest approach is to add a helper method to `ICompanyRoleRepository`:

```csharp
// Add to ICompanyRoleRepository interface:
Task ClearPermissionsAsync(Guid roleId, CancellationToken ct = default);
```

```csharp
// Add to CompanyRoleRepository implementation:
public async Task ClearPermissionsAsync(Guid roleId, CancellationToken ct = default)
{
    var permissions = await _context.Set<CompanyRolePermission>()
        .Where(p => p.RoleId == roleId)
        .ToListAsync(ct);
    _context.Set<CompanyRolePermission>().RemoveRange(permissions);
}
```

Then in `UpdateCompanyRoleCommandHandler`, replace the permissions section with:

```csharp
await _unitOfWork.CompanyRoles.ClearPermissionsAsync(role.Id, cancellationToken);

foreach (var permission in request.Permissions.Distinct())
{
    var perm = new CompanyRolePermission { RoleId = role.Id, Permission = permission };
    await _unitOfWork.Context.Set<CompanyRolePermission>().AddAsync(perm, cancellationToken);
    // OR: _context.CompanyRolePermissions.Add(perm) if you have the DbContext injected
}
```

> **Simplest approach**: Add `ICompanyRoleRepository.ClearPermissionsAsync` and inject `ApplicationDbContext` into `CompanyRoleRepository` (it already is, via `Repository<T>`). Use `_context.CompanyRolePermissions.RemoveRange(...)` and `_context.CompanyRolePermissions.AddRange(...)` in the handler — or entirely within the repository method `ReplacePermissionsAsync(Guid roleId, IEnumerable<string> newPermissions)`.

---

## Step 6 — Domain Errors

### 6a. MODIFY: `HrSystemApp.Application/Errors/DomainErrors.cs`

Add a new `Roles` static class and extend the `Request` static class:

```csharp
public static class Roles
{
    public static readonly Error NotFound =
        new("Roles.NotFound", "The role was not found.");

    public static readonly Error NameAlreadyExists =
        new("Roles.NameAlreadyExists", "A role with this name already exists in the company.");

    public static readonly Error InUseByWorkflow =
        new("Roles.InUseByWorkflow", "This role is referenced by an active workflow definition and cannot be deleted.");

    public static readonly Error AlreadyAssigned =
        new("Roles.AlreadyAssigned", "This role is already assigned to the employee.");

    public static readonly Error AssignmentNotFound =
        new("Roles.AssignmentNotFound", "This role assignment does not exist.");

    public static readonly Error InvalidPermission =
        new("Roles.InvalidPermission", "One or more permissions are not valid. Check AppPermissions for allowed values.");
}
```

Also add to the existing `Request` static class (alongside existing errors):

```csharp
public static readonly Error MissingCompanyRoleId =
    new("Request.MissingCompanyRoleId", "CompanyRoleId is required for CompanyRole workflow steps.");

public static readonly Error RoleNotFound =
    new("Request.RoleNotFound", "The referenced role does not exist or has been deleted.");

public static readonly Error RoleNotInCompany =
    new("Request.RoleNotInCompany", "The referenced role does not belong to this company.");

public static readonly Error MissingLevelsUp =
    new("Request.MissingLevelsUp", "HierarchyLevel steps must specify LevelsUp >= 1.");
```

> **Note**: `MissingLevelsUp` may already exist — check before adding.

---

## Step 7 — Workflow Integration

### 7a. MODIFY: `HrSystemApp.Application/Interfaces/Repositories/IRequestDefinitionRepository.cs`

Add one new method:

```csharp
/// <summary>
/// Returns true if any non-deleted RequestDefinition for this company
/// has a workflow step referencing the given CompanyRole ID.
/// Used to block deleting a role that is still in use.
/// </summary>
Task<bool> IsRoleInUseAsync(Guid companyRoleId, CancellationToken ct = default);
```

### 7b. MODIFY: `HrSystemApp.Infrastructure/Repositories/RequestDefinitionRepository.cs`

Implement the new method:

```csharp
public async Task<bool> IsRoleInUseAsync(Guid companyRoleId, CancellationToken ct = default)
{
    return await _context.Set<RequestWorkflowStep>()
        .AnyAsync(s => s.CompanyRoleId == companyRoleId, ct);
}
```

> Note: `RequestWorkflowStep` rows are not soft-deleted (they are owned by `RequestDefinition`). Query them directly via `_context.Set<RequestWorkflowStep>()`. If the definition is hard-deleted, its steps are cascade-deleted, so this query is safe.

### 7c. MODIFY: `HrSystemApp.Application/Features/Requests/Commands/Admin/CreateRequestDefinitionCommand.cs`

In the per-step validation loop (around line 133 where `OrgNode` and `DirectEmployee` are validated), add a new `else if` branch:

```csharp
else if (step.StepType == WorkflowStepType.CompanyRole)
{
    if (!step.CompanyRoleId.HasValue)
        return Result.Failure<Guid>(DomainErrors.Request.MissingCompanyRoleId);

    var role = await _unitOfWork.CompanyRoles.GetByIdAsync(step.CompanyRoleId.Value, cancellationToken);
    if (role is null || role.CompanyId != targetCompanyId)
        return Result.Failure<Guid>(DomainErrors.Request.RoleNotInCompany);
}
```

Also in the field consistency validation (around line 81–103 where `HierarchyLevel` vs other step types are checked), add `CompanyRole` to the "must NOT have `StartFromLevel` or `LevelsUp`" branch:

```csharp
else // OrgNode, DirectEmployee, CompanyRole
{
    if (step.StartFromLevel.HasValue || step.LevelsUp.HasValue)
        return Result.Failure<Guid>(DomainErrors.Request.HierarchyLevelFieldsOnNonHierarchyStep);
}
```

When mapping `WorkflowStepDto` → `RequestWorkflowStep` entity (the mapping that creates the DB row), copy `CompanyRoleId`:

```csharp
new RequestWorkflowStep
{
    RequestDefinitionId = definition.Id,
    StepType = step.StepType,
    OrgNodeId = step.OrgNodeId,
    BypassHierarchyCheck = step.BypassHierarchyCheck,
    DirectEmployeeId = step.DirectEmployeeId,
    StartFromLevel = step.StartFromLevel,
    LevelsUp = step.LevelsUp,
    CompanyRoleId = step.CompanyRoleId,   // ← add this line
    SortOrder = step.SortOrder
}
```

### 7d. MODIFY: `HrSystemApp.Application/Features/Requests/Commands/Admin/UpdateRequestDefinitionCommand.cs`

Apply the same two changes as 7c (validation branch + mapping `CompanyRoleId` when rebuilding steps).

### 7e. MODIFY: `HrSystemApp.Application/Features/Requests/Commands/CreateRequest/CreateRequestCommand.cs`

**Change 1**: In the submission-time validation loop (around line 121), add after the `DirectEmployee` branch:

```csharp
else if (step.StepType == WorkflowStepType.CompanyRole)
{
    if (!step.CompanyRoleId.HasValue)
        return Result.Failure<Guid>(DomainErrors.Request.MissingCompanyRoleId);

    var role = await _unitOfWork.CompanyRoles.GetByIdAsync(step.CompanyRoleId.Value, cancellationToken);
    if (role is null || role.IsDeleted || role.CompanyId != employee.CompanyId)
        return Result.Failure<Guid>(DomainErrors.Request.RoleNotInCompany);
}
```

**Change 2**: In the `WorkflowStepDto` mapping block (around line 106–117), add `CompanyRoleId`:

```csharp
var definitionSteps = definition.WorkflowSteps
    .Select(s => new WorkflowStepDto
    {
        StepType = s.StepType,
        OrgNodeId = s.OrgNodeId,
        BypassHierarchyCheck = s.BypassHierarchyCheck,
        DirectEmployeeId = s.DirectEmployeeId,
        StartFromLevel = s.StartFromLevel,
        LevelsUp = s.LevelsUp,
        CompanyRoleId = s.CompanyRoleId,   // ← add this line
        SortOrder = s.SortOrder
    })
    .ToList();
```

### 7f. MODIFY: `HrSystemApp.Infrastructure/Services/WorkflowResolutionService.cs`

In `BuildApprovalChainAsync`, inside the `foreach (var step in sortedSteps)` loop, add a new branch **after** the `HierarchyLevel` block and **before** the final `else` (OrgNode) block:

```csharp
else if (step.StepType == WorkflowStepType.CompanyRole)
{
    // ── COMPANY ROLE STEP ─────────────────────────────────────────────
    if (!step.CompanyRoleId.HasValue)
        return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingCompanyRoleId);

    var role = await _unitOfWork.CompanyRoles.GetByIdAsync(step.CompanyRoleId.Value, ct);
    if (role is null)
        return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.RoleNotFound);

    // Get all active employees with this role
    var roleHolders = await _unitOfWork.EmployeeCompanyRoles
        .GetActiveEmployeesByRoleIdAsync(step.CompanyRoleId.Value, ct);

    // Exclude the requester (self-approval prevention) and dedup against earlier steps
    var approvers = roleHolders
        .Where(e => e.Id != requesterEmployeeId && !seenApproverIds.Contains(e.Id))
        .Select(e => new ApproverDto { EmployeeId = e.Id, EmployeeName = e.FullName })
        .ToList();

    if (approvers.Count == 0)
    {
        // Gracefully skip — no eligible approvers (same behavior as hierarchy levels
        // with no managers assigned). The step simply does not appear in the chain.
        _logger.LogInformation(
            "CompanyRole step {SortOrder} (role: {RoleName}) skipped: no eligible role holders",
            step.SortOrder, role.Name);
        continue;
    }

    foreach (var a in approvers) seenApproverIds.Add(a.EmployeeId);

    plannedSteps.Add(new PlannedStepDto
    {
        StepType = WorkflowStepType.CompanyRole,
        NodeId = null,
        NodeName = role.Name,
        CompanyRoleId = role.Id,
        RoleName = role.Name,
        SortOrder = 0, // renumbered at end by the existing renumber loop
        Approvers = approvers
    });
}
```

**"First one wins" is automatic** with no changes to `ApproveRequestCommand`. Here is why:
- At submission time, all role-holder employee IDs are snapshotted into `PlannedStepsJson` as `Approvers` and into `CurrentStepApproverIds` as a comma-separated string.
- `ApproveRequestCommand` checks `approverIds.Contains(employee.Id)` — any of the role holders can call this endpoint.
- The first one to approve calls `existingRequest.CurrentStepOrder++` which moves to the next step, and `CurrentStepApproverIds` is updated to the *next step's* approvers.
- All other role holders who query "my pending requests" filter by `CurrentStepApproverIds LIKE '%{employeeId}%'` — their ID is no longer there, so the request disappears from their view.

---

## Step 8 — Authorization (Permissions System)

### 8a. NEW: `HrSystemApp.Application/Authorization/PermissionRequirement.cs`

```csharp
using Microsoft.AspNetCore.Authorization;

namespace HrSystemApp.Application.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }
}
```

### 8b. NEW: `HrSystemApp.Infrastructure/Authorization/PermissionAuthorizationHandler.cs`

```csharp
using HrSystemApp.Application.Authorization;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;

namespace HrSystemApp.Infrastructure.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public PermissionAuthorizationHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return;

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, default);
        if (employee is null)
            return;

        var permissions = await _unitOfWork.EmployeeCompanyRoles
            .GetPermissionsForEmployeeAsync(employee.Id, default);

        if (permissions.Contains(requirement.Permission))
            context.Succeed(requirement);
    }
}
```

### 8c. MODIFY: `HrSystemApp.Infrastructure/DependencyInjection.cs`

Add the following inside the `AddInfrastructure` (or equivalent) method, after the existing `services.AddScoped<...>()` registrations:

```csharp
// Permission-based authorization
services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

services.AddAuthorization(options =>
{
    foreach (var permission in HrSystemApp.Domain.Constants.AppPermissions.All)
    {
        options.AddPolicy(permission, policy =>
            policy.AddRequirements(new HrSystemApp.Application.Authorization.PermissionRequirement(permission)));
    }
});
```

**Usage on controller actions:**
```csharp
[Authorize(Policy = AppPermissions.ViewAllAttendance)]
public async Task<IActionResult> GetCompanyAttendance(...) { ... }
```

---

## Step 9 — API Controller

### 9a. NEW: `HrSystemApp.Api/Controllers/CompanyRolesController.cs`

```csharp
using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.Features.Roles.Commands.AssignRoleToEmployee;
using HrSystemApp.Application.Features.Roles.Commands.CreateCompanyRole;
using HrSystemApp.Application.Features.Roles.Commands.DeleteCompanyRole;
using HrSystemApp.Application.Features.Roles.Commands.RemoveRoleFromEmployee;
using HrSystemApp.Application.Features.Roles.Commands.UpdateCompanyRole;
using HrSystemApp.Application.Features.Roles.Queries.GetCompanyRoleById;
using HrSystemApp.Application.Features.Roles.Queries.GetCompanyRoles;
using HrSystemApp.Application.Features.Roles.Queries.GetEmployeeRoles;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Authorize(Roles = Roles.HrOrAbove)]
[Route("api/company-roles")]
public class CompanyRolesController : BaseApiController
{
    private readonly ISender _sender;

    public CompanyRolesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Returns all custom roles for the caller's company.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(new GetCompanyRolesQuery(), cancellationToken));
    }

    /// <summary>Returns a single role with its permissions.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(new GetCompanyRoleByIdQuery(id), cancellationToken));
    }

    /// <summary>Creates a new custom role.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCompanyRoleRequest request, CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(
            new CreateCompanyRoleCommand(request.Name, request.Description, request.Permissions),
            cancellationToken));
    }

    /// <summary>Updates an existing role's name, description, and permissions.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateCompanyRoleRequest request, CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(
            new UpdateCompanyRoleCommand(id, request.Name, request.Description, request.Permissions),
            cancellationToken));
    }

    /// <summary>Soft-deletes a role. Blocked if role is referenced by an active workflow.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(new DeleteCompanyRoleCommand(id), cancellationToken));
    }

    /// <summary>Returns all roles assigned to a specific employee.</summary>
    [HttpGet("{roleId:guid}/employees")]
    public async Task<IActionResult> GetRoleEmployees(
        Guid roleId, CancellationToken cancellationToken)
    {
        // This can call a GetEmployeesByRoleQuery if you implement it,
        // or reuse existing employee list filtered by role — extend as needed.
        return Ok(); // placeholder — implement GetEmployeesByRoleQuery if needed
    }

    /// <summary>Assigns a custom role to an employee.</summary>
    [HttpPost("{roleId:guid}/employees/{employeeId:guid}")]
    public async Task<IActionResult> AssignToEmployee(
        Guid roleId, Guid employeeId, CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(
            new AssignRoleToEmployeeCommand(employeeId, roleId),
            cancellationToken));
    }

    /// <summary>Removes a custom role from an employee.</summary>
    [HttpDelete("{roleId:guid}/employees/{employeeId:guid}")]
    public async Task<IActionResult> RemoveFromEmployee(
        Guid roleId, Guid employeeId, CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(
            new RemoveRoleFromEmployeeCommand(employeeId, roleId),
            cancellationToken));
    }

    /// <summary>Returns all custom roles assigned to a given employee.</summary>
    [HttpGet("by-employee/{employeeId:guid}")]
    public async Task<IActionResult> GetByEmployee(Guid employeeId, CancellationToken cancellationToken)
    {
        return HandleResult(await _sender.Send(
            new GetEmployeeRolesQuery(employeeId), cancellationToken));
    }
}

// Request models (keep at bottom of file)
public sealed record CreateCompanyRoleRequest(
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions);

public sealed record UpdateCompanyRoleRequest(
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions);
```

---

## Step 10 — Verification Checklist

After implementing everything, verify each of the following before declaring the feature complete:

1. **Build passes** with zero errors. The main sources of compilation errors will be:
   - Missing `using` directives for `HrSystemApp.Domain.Constants` (for `AppPermissions`)
   - Missing `CompanyRoleId` in places where `RequestWorkflowStep` is mapped
   - `IUnitOfWork` not implementing the two new properties

2. **Migration**: Run `dotnet ef migrations add AddCompanyRoles` and confirm three new tables + one new nullable column are created. Apply with `dotnet ef database update`.

3. **CreateCompanyRole**: POST to `/api/company-roles` creates a role and returns a GUID.

4. **UpdateCompanyRole**: PUT replaces permissions entirely (old permissions gone, new ones present).

5. **DeleteCompanyRole**: DELETE soft-deletes the role. A second call to GET `/api/company-roles/{id}` returns 404. A call to GET `/api/company-roles` does not include it.

6. **Workflow definition with CompanyRole step**: POST to `/api/request-definitions` with a step of `StepType: 3` and a valid `CompanyRoleId` succeeds.

7. **Request submission**: POST to `/api/requests` with a type that has a CompanyRole step builds the approval chain correctly — `PlannedStepsJson` contains a step with `StepType: 3` and `Approvers` containing all active employees with that role.

8. **First-one-wins**: Two employees with the same role, request at a CompanyRole step. Employee A approves → step advances. Employee B calls GET `/api/requests/pending` → request no longer appears.

9. **Permission authorization**: An employee with a role that has `attendance.view_all` permission can call an endpoint protected by `[Authorize(Policy = AppPermissions.ViewAllAttendance)]`. An employee without it gets 403.

10. **Role-in-use guard**: Attempting to delete a role referenced by an active workflow definition returns `Roles.InUseByWorkflow` error.

---

## Summary of Files

| Action | File |
|--------|------|
| CREATE | `Domain/Models/CompanyRole.cs` |
| CREATE | `Domain/Models/CompanyRolePermission.cs` |
| CREATE | `Domain/Models/EmployeeCompanyRole.cs` |
| CREATE | `Domain/Constants/AppPermissions.cs` |
| CREATE | `Infrastructure/Data/Configurations/CompanyRoleConfiguration.cs` |
| CREATE | `Infrastructure/Data/Configurations/CompanyRolePermissionConfiguration.cs` |
| CREATE | `Infrastructure/Data/Configurations/EmployeeCompanyRoleConfiguration.cs` |
| CREATE | `Infrastructure/Repositories/CompanyRoleRepository.cs` |
| CREATE | `Infrastructure/Repositories/EmployeeCompanyRoleRepository.cs` |
| CREATE | `Infrastructure/Authorization/PermissionAuthorizationHandler.cs` |
| CREATE | `Application/Interfaces/Repositories/ICompanyRoleRepository.cs` |
| CREATE | `Application/Interfaces/Repositories/IEmployeeCompanyRoleRepository.cs` |
| CREATE | `Application/Authorization/PermissionRequirement.cs` |
| CREATE | `Application/DTOs/Roles/CompanyRoleDto.cs` |
| CREATE | `Application/Features/Roles/Commands/CreateCompanyRole/CreateCompanyRoleCommand.cs` |
| CREATE | `Application/Features/Roles/Commands/UpdateCompanyRole/UpdateCompanyRoleCommand.cs` |
| CREATE | `Application/Features/Roles/Commands/DeleteCompanyRole/DeleteCompanyRoleCommand.cs` |
| CREATE | `Application/Features/Roles/Commands/AssignRoleToEmployee/AssignRoleToEmployeeCommand.cs` |
| CREATE | `Application/Features/Roles/Commands/RemoveRoleFromEmployee/RemoveRoleFromEmployeeCommand.cs` |
| CREATE | `Application/Features/Roles/Queries/GetCompanyRoles/GetCompanyRolesQuery.cs` |
| CREATE | `Application/Features/Roles/Queries/GetCompanyRoleById/GetCompanyRoleByIdQuery.cs` |
| CREATE | `Application/Features/Roles/Queries/GetEmployeeRoles/GetEmployeeRolesQuery.cs` |
| CREATE | `Api/Controllers/CompanyRolesController.cs` |
| MODIFY | `Domain/Enums/WorkflowStepType.cs` — add `CompanyRole = 3` |
| MODIFY | `Domain/Models/RequestWorkflow.cs` — add `CompanyRoleId` + nav to `RequestWorkflowStep` |
| MODIFY | `Domain/Models/Employee.cs` — add `CompanyRoles` navigation |
| MODIFY | `Infrastructure/Data/ApplicationDbContext.cs` — add 3 DbSets |
| MODIFY | `Infrastructure/Data/Configurations/RequestWorkflowStepConfiguration.cs` — add FK |
| MODIFY | `Application/Interfaces/IUnitOfWork.cs` — add 2 repo properties |
| MODIFY | `Infrastructure/Repositories/UnitOfWork.cs` — wire 2 repos |
| MODIFY | `Application/DTOs/Requests/WorkflowStepDto.cs` — add `CompanyRoleId` (rewrite full file) |
| MODIFY | `Application/DTOs/Requests/PlannedStepDto.cs` — add `CompanyRoleId`, `RoleName` (rewrite full file) |
| MODIFY | `Application/Errors/DomainErrors.cs` — add `Roles` class + `Request.*` entries |
| MODIFY | `Application/Interfaces/Repositories/IRequestDefinitionRepository.cs` — add `IsRoleInUseAsync` |
| MODIFY | `Infrastructure/Repositories/RequestDefinitionRepository.cs` — implement it |
| MODIFY | `Application/Features/Requests/Commands/Admin/CreateRequestDefinitionCommand.cs` — CompanyRole validation + mapping |
| MODIFY | `Application/Features/Requests/Commands/Admin/UpdateRequestDefinitionCommand.cs` — same |
| MODIFY | `Application/Features/Requests/Commands/CreateRequest/CreateRequestCommand.cs` — role validation + copy CompanyRoleId |
| MODIFY | `Infrastructure/Services/WorkflowResolutionService.cs` — add CompanyRole branch |
| MODIFY | `Infrastructure/DependencyInjection.cs` — register handler + policies |
| RUN    | `dotnet ef migrations add AddCompanyRoles` + `dotnet ef database update` |
