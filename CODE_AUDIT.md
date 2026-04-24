# HrSystemApp — Full Codebase Audit
**Date:** 2026-04-24  
**Auditor:** Claude (Anthropic)  
**Scope:** All layers — Domain, Application, Infrastructure, API  
**Stack:** .NET 8 · EF Core · PostgreSQL · MediatR / CQRS · Clean Architecture

---

## Table of Contents

1. [Critical Issues](#critical-issues)
2. [High Issues](#high-issues)
3. [Medium Issues](#medium-issues)
4. [Low / Code Quality](#low--code-quality)
5. [WorkflowResolutionService Refactor Findings](#workflowresolutionservice-refactor-findings)
6. [Priority Summary Table](#priority-summary-table)

---

## Critical Issues

---

### C-1 · `GetAttendanceSessions` — Ownership check always returns false

**File:** `HrSystemApp.Application/Features/Attendance/Queries/GetAttendanceSessions/GetAttendanceSessionsQuery.cs` line 43  
**Severity:** 🔴 Critical — Broken feature + incorrect authorization  

**Problem:**
```csharp
var isOwner = attendance.EmployeeId.ToString() == currentUserId;
```

`attendance.EmployeeId` is the `Employee` table primary key (a `Guid`). `currentUserId` from `ICurrentUserService` is the ASP.NET Identity `ApplicationUser.Id` (a completely different string from a different table). These two identifiers will **never match**. As a result, every regular employee who calls `GET /attendance/{id}/sessions` for their own record receives `403 Forbidden`. Only HR+ users can access the endpoint — the feature is completely broken for the users it was built for.

**Fix:**
```csharp
var currentEmployee = await _unitOfWork.Employees.GetByUserIdAsync(currentUserId, cancellationToken);
var isOwner = currentEmployee?.Id == attendance.EmployeeId;
```

---

### C-2 · `Repository.GetByIdAsync` bypasses the global soft-delete query filter

**File:** `HrSystemApp.Infrastructure/Repositories/Repository.cs` line 23  
**Severity:** 🔴 Critical — Systemic data integrity issue across all repositories  

**Problem:**
```csharp
public virtual async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
{
    return await _dbSet.FindAsync(new object?[] { id }, cancellationToken);
}
```

`DbSet.FindAsync` uses EF Core's identity cache and **does not apply global query filters**, including the `!IsDeleted` soft-delete filter registered in `ApplicationDbContext.ApplySoftDeleteQueryFilter`. Any handler that calls `GetByIdAsync` — attendance, employees, requests, org nodes, company roles, etc. — can silently receive soft-deleted entities.

Since `GetByIdAsync` is the primary lookup used across every feature, this is systemic. For example:
- A deleted employee can be fetched and used to build an approval chain.
- A deleted org node can be used to route requests.
- A deleted request can be fetched and approved.

**Fix:** Replace `FindAsync` with a filtered query:
```csharp
public virtual async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
{
    // FindAsync bypasses global query filters (e.g. soft-delete).
    // Use FirstOrDefaultAsync so the !IsDeleted filter is applied.
    return await _dbSet.FirstOrDefaultAsync(
        e => EF.Property<object>(e, "Id").Equals(id),
        cancellationToken);
}
```

---

### C-3 · `GET admin/company-wide` — No role guard, any employee can access

**File:** `HrSystemApp.Api/Controllers/RequestsController.cs` line 136  
**Severity:** 🔴 Critical — Broken access control  

**Problem:**
```csharp
[HttpGet("admin/company-wide")]
public async Task<IActionResult> GetCompanyWideRequests([FromQuery] GetCompanyRequestsQuery query)
{
    return HandleResult(await _sender.Send(query));
}
```

No `[Authorize(Roles = ...)]` attribute is present on this endpoint. The handler (`GetCompanyRequestsQueryHandler`) also does not check the caller's role — it only verifies the user has an employee record. Any authenticated regular employee can call this endpoint and enumerate every other employee's requests, statuses, approval history, and details for the entire company.

**Fix:** Add a role guard on the controller action:
```csharp
[HttpGet("admin/company-wide")]
[Authorize(Roles = Roles.HrOrAbove)]
public async Task<IActionResult> GetCompanyWideRequests([FromQuery] GetCompanyRequestsQuery query)
{
    return HandleResult(await _sender.Send(query));
}
```

---

### C-4 · `GetRequestById` — HR users can read requests from other companies

**File:** `HrSystemApp.Application/Features/Requests/Queries/GetRequestById/GetRequestByIdQuery.cs`  
**Severity:** 🔴 Critical — Cross-tenant data leak  

**Problem:**
```csharp
var isHrOrAbove = currentUserRole is not null &&
    Enum.TryParse<UserRole>(currentUserRole, out var role) &&
    role is UserRole.SuperAdmin or UserRole.Executive or UserRole.HR or UserRole.CompanyAdmin;

// ...

if (!isHrOrAbove && !isRequester && !isApprover)
    return Result.Failure<RequestDetailDto>(DomainErrors.Auth.Unauthorized);
```

The `isHrOrAbove` flag is derived solely from the user's role claim with no company scoping. An HR user from Company A can query any `Request.Id` by GUID and, because `isHrOrAbove` is `true`, the check passes regardless of which company owns the request. The handler never verifies `existingRequest.Employee.CompanyId == callerEmployee.CompanyId`.

**Fix:** Add a company check after the HR role check:
```csharp
if (isHrOrAbove)
{
    var callerEmployee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
    if (callerEmployee?.CompanyId != existingRequest.Employee.CompanyId)
        return Result.Failure<RequestDetailDto>(DomainErrors.Auth.Unauthorized);
}
```

---

### C-5 · `OverrideClockIn` / `OverrideClockOut` — No company isolation on target employee

**Files:**
- `HrSystemApp.Application/Features/Attendance/Commands/OverrideClockIn/OverrideClockInCommand.cs`
- `HrSystemApp.Application/Features/Attendance/Commands/OverrideClockOut/OverrideClockOutCommand.cs`

**Severity:** 🔴 Critical — Cross-tenant data manipulation  

**Problem:** Both handlers accept an `EmployeeId` from the request body and operate directly on that employee's attendance record. Neither handler verifies that the target employee belongs to the same company as the calling HR user. An HR admin from Company A can supply any `EmployeeId` and manipulate attendance records for employees in Company B.

**Fix:** Resolve the calling user's employee and verify company match:
```csharp
var callerUserId = _currentUserService.UserId;
var callerEmployee = await _unitOfWork.Employees.GetByUserIdAsync(callerUserId, cancellationToken);

var targetEmployee = await _unitOfWork.Employees.GetByIdAsync(request.EmployeeId, cancellationToken);
if (targetEmployee == null)
    return Result.Failure<AttendanceResponse>(DomainErrors.Employee.NotFound);

if (callerEmployee?.CompanyId != targetEmployee.CompanyId)
    return Result.Failure<AttendanceResponse>(DomainErrors.Auth.Unauthorized);
```

---

## High Issues

---

### H-1 · Four query handlers load all rows into memory before paginating

**Files:**
- `GetUserRequestsQuery.cs`
- `GetCompanyRequestsQuery.cs`
- `GetPendingApprovalsQuery.cs`
- `GetMyApprovalActionsQuery.cs`

**Severity:** 🟠 High — Performance, potential OOM on large datasets  

**Problem:** All four handlers follow the same broken pattern:
```csharp
// Loads ALL matching rows from the database into a List<T>
var requests = await _unitOfWork.Requests.FindAsync(predicate, cancellationToken);

// .AsQueryable() on a List<T> is LINQ-to-Objects, NOT LINQ-to-EF
var queryable = requests.AsQueryable();

// Filtering, sorting, and pagination all happen in-memory
if (request.Status.HasValue)
    queryable = queryable.Where(r => r.Status == request.Status.Value);

var items = queryable.OrderByDescending(r => r.CreatedAt)
    .Skip((request.PageNumber - 1) * request.PageSize)
    .Take(request.PageSize)
    .ToList();
```

`FindAsync` returns a `List<T>`. Calling `.AsQueryable()` on a list **does not produce a database query** — it wraps the already-loaded in-memory list. On a company with 50,000 requests, every paginated call to page 1 with `pageSize=10` loads all 50,000 records into memory.

**Fix:** Push filtering, sorting, and pagination to the database. Each repository method should accept filter and pagination parameters and translate them to EF Core queries with `.Where()`, `.OrderBy()`, `.Skip()`, `.Take()`, and a projected `.CountAsync()` — all resolved at the database level before returning results.

---

### H-2 · `CreateRequest` — N+1 queries in workflow step validation loop

**File:** `HrSystemApp.Application/Features/Requests/Commands/CreateRequest/CreateRequestCommand.cs` lines 119–165  
**Severity:** 🟠 High — Performance  

**Problem:**
```csharp
foreach (var step in definitionSteps)
{
    if (step.StepType == WorkflowStepType.OrgNode && step.OrgNodeId.HasValue)
    {
        var node = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId.Value, ct); // 1 DB call per step
    }
    else if (step.StepType == WorkflowStepType.DirectEmployee && step.DirectEmployeeId.HasValue)
    {
        var directEmp = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, ct); // 1 DB call per step
    }
    else if (step.StepType == WorkflowStepType.CompanyRole)
    {
        var role = await _unitOfWork.CompanyRoles.GetByIdAsync(step.CompanyRoleId.Value, ct); // 1 DB call per step
    }
}
```

This fires up to one DB call per workflow step per type. A definition with 6 steps (2 OrgNode + 2 DirectEmployee + 2 CompanyRole) results in 6 sequential DB calls just for pre-validation — before `BuildApprovalChainAsync` runs (which makes its own DB calls). This is the same N+1 pattern that was fixed in `WorkflowResolutionService` and needs the same batch-fetch treatment here.

**Fix:** Collect all IDs by type first, then batch-fetch in parallel:
```csharp
var orgNodeIds  = definitionSteps.Where(s => s.StepType == WorkflowStepType.OrgNode && s.OrgNodeId.HasValue)
                                  .Select(s => s.OrgNodeId!.Value).Distinct().ToList();
var employeeIds = definitionSteps.Where(s => s.StepType == WorkflowStepType.DirectEmployee && s.DirectEmployeeId.HasValue)
                                  .Select(s => s.DirectEmployeeId!.Value).Distinct().ToList();
var roleIds     = definitionSteps.Where(s => s.StepType == WorkflowStepType.CompanyRole && s.CompanyRoleId.HasValue)
                                  .Select(s => s.CompanyRoleId!.Value).Distinct().ToList();

// Batch fetch all in parallel
var (nodes, employees, roles) = await FetchStepEntitiesAsync(orgNodeIds, employeeIds, roleIds, ct);

// Validate using the in-memory dictionaries
foreach (var step in definitionSteps) { ... }
```

---

### H-3 · `OverrideClockIn` returns wrong error code for future timestamp

**File:** `HrSystemApp.Application/Features/Attendance/Commands/OverrideClockIn/OverrideClockInCommand.cs`  
**Severity:** 🟠 High — Incorrect API contract, misleading client errors  

**Problem:**
```csharp
if (request.ClockInUtc > DateTime.UtcNow.AddMinutes(5))
{
    return Result.Failure<AttendanceResponse>(DomainErrors.Attendance.InvalidClockOut); // ← Wrong error
}
```

A validation failure for an invalid **clock-in** timestamp returns `DomainErrors.Attendance.InvalidClockOut`. The client receives a domain error that refers to a clock-out when the actual problem is the clock-in time. This causes confusion for both API consumers and support debugging.

**Fix:**
```csharp
if (request.ClockInUtc > DateTime.UtcNow.AddMinutes(5))
    return Result.Failure<AttendanceResponse>(DomainErrors.Attendance.InvalidClockIn);
```
(Add `InvalidClockIn` to `DomainErrors.Attendance` if it doesn't yet exist.)

---

## Medium Issues

---

### M-1 · `GetUserRequests`, `GetCompanyRequests`, `GetPendingApprovals` — No `PageSize` upper bound

**Files:** All three query record definitions  
**Severity:** 🟡 Medium — Potential denial of service  

**Problem:**
```csharp
public int PageSize { get; set; } = 10; // No maximum cap
```

A client can pass `?pageSize=1000000`. The existing `PaginationParams` class already solves this with `MaxPageSize = 100`, but none of these queries inherit from or use it. Given that these queries also load everything to memory first (H-1), an unbounded `pageSize` amplifies the memory impact.

**Fix:** Either inherit from `PaginationParams` or apply the cap inline:
```csharp
private const int MaxPageSize = 100;
private int _pageSize = 10;
public int PageSize
{
    get => _pageSize;
    set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
}
```

---

### M-2 · Deprecated `WorkflowService` still registered in DI

**File:** `HrSystemApp.Infrastructure/DependencyInjection.cs` line 127  
**Severity:** 🟡 Medium — Silent security regression risk  

**Problem:**
```csharp
services.AddScoped<IWorkflowService, WorkflowService>();
```

`WorkflowService` is marked `[Obsolete]` and its `GetApprovalPathAsync` returns an empty `List<Employee>` with only a warning log. If any code — current or future — accidentally injects `IWorkflowService` instead of `IWorkflowResolutionService`, the entire approval chain silently returns nothing, causing every request to be auto-approved with no approvers. This is a latent security regression vector.

**Fix:** Remove the registration entirely. If the interface is needed for compilation, also delete `IWorkflowService` and `WorkflowService`.

---

### M-3 · Multiple `SaveChangesAsync` calls per transaction in clock commands

**Files:**
- `ClockInCommand.cs`
- `ClockOutCommand.cs`
- `OverrideClockInCommand.cs`
- `OverrideClockOutCommand.cs`

**Severity:** 🟡 Medium — Unnecessary DB round trips  

**Problem:** Each handler calls `SaveChangesAsync` 2–3 times within a single `BeginTransactionAsync / CommitTransactionAsync` block:
```csharp
await _unitOfWork.Attendances.AddAsync(attendance, ct);
await _unitOfWork.SaveChangesAsync(ct);   // Round trip 1: to get attendance.Id for FK

await _unitOfWork.AttendanceLogs.AddAsync(log, ct);
await _unitOfWork.SaveChangesAsync(ct);   // Round trip 2

await _unitOfWork.Attendances.UpdateAsync(attendance, ct);
await _unitOfWork.SaveChangesAsync(ct);   // Round trip 3
await _unitOfWork.CommitTransactionAsync(ct);
```

The first save exists to populate `attendance.Id` for the `AttendanceLog.AttendanceId` foreign key. EF Core's graph tracking can resolve this automatically by assigning the `Attendance` navigation property instead of the raw FK, allowing all entities to be saved in a single `SaveChangesAsync` call.

---

### M-4 · `GetRequestById.Data` field typed as `object`

**File:** `HrSystemApp.Application/Features/Requests/Queries/GetRequestById/GetRequestByIdQuery.cs`  
**Severity:** 🟡 Medium — Untyped API contract, potential information disclosure  

**Problem:**
```csharp
// DTO field:
public object Data { get; set; } = new { };

// Handler:
Data = JsonSerializer.Deserialize<object>(existingRequest.Data) ?? new { },
```

Deserializing to `object` produces a boxed `JsonElement`. This is then serialized back to the client as arbitrary JSON with no schema enforcement or sanitization. If `existingRequest.Data` contains sensitive fields the requesting user should not see (e.g., salary details in a pay-raise request viewed by an approver), they are fully exposed. There is also no API contract for consumers.

---

## Low / Code Quality

---

### L-1 · `RequireHttpsMetadata = false` should be environment-gated

**File:** `HrSystemApp.Api/DependencyInjection.cs`  

```csharp
options.RequireHttpsMetadata = false;
```

Acceptable for development and mobile backends, but should be restricted to non-production:
```csharp
options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
```

---

### L-2 · CORS `AllowAll` policy accepts any origin

**File:** `HrSystemApp.Api/Program.cs`  

`SetIsOriginAllowed(_ => true)` accepts requests from any origin. Acceptable for mobile-first development, but should be tightened in production using environment-specific config for known origins (web app domain, deep-link schemes).

---

### L-3 · `TokenService` is `AddScoped` but holds no request-scoped state

**File:** `HrSystemApp.Infrastructure/DependencyInjection.cs`  

`TokenService` reads configuration and performs stateless crypto operations. It holds no per-request state and recreates `SymmetricSecurityKey` on every `GenerateToken` call. It should be `AddSingleton` and cache the key and parsed settings in the constructor.

---

### L-4 · `IWorkflowService` interface and `WorkflowService` implementation are dead code

**File:** `HrSystemApp.Infrastructure/Services/WorkflowService.cs`  

The class is `[Obsolete]`, returns empty results, and serves no functional purpose. The interface, implementation, and DI registration should all be removed.

---

## WorkflowResolutionService Refactor Findings

These were identified during the refactor of `WorkflowResolutionService` into the Strategy Pattern. Two have already been fixed.

---

### WF-1 · ✅ FIXED — `BatchFetchRelatedDataAsync` used employee ID as the role holders dictionary key

**File:** `WorkflowResolutionService.cs`  

Was using `holders[0].Id` (an employee's `Guid`) as the key in `RoleHoldersByRoleId`. The lookup in `CompanyRoleStepResolver` used `step.CompanyRoleId` (the role's `Guid`). These never matched, so all `CompanyRole` approval steps silently produced zero approvers. Fixed with `Zip` to preserve `roleId ↔ holders` ordering.

---

### WF-2 · ✅ FIXED — `TryAddStep` had a partial-write on collision

**File:** `WorkflowResolutionContext.cs`  

When iterating approvers, if a mid-loop approver was already seen, the method returned `false` without undoing the `.Add()` calls for the preceding approvers in the same loop. Fixed by separating validation (`Any(...)`) from mutation (`foreach Add`) into two distinct passes.

---

### WF-3 · `SeenApproverIds` exposed as mutable `HashSet<Guid>`

**File:** `WorkflowResolutionContext.cs`  

`WorkflowResolutionState.SeenApproverIds` is a public `HashSet<Guid>`. External code can call `.Add()` or `.Remove()` directly, bypassing `TryAddStep` and breaking the invariant that `SeenApproverIds` and `PlannedSteps` stay in sync. Should be backed by a private field and exposed as `IReadOnlySet<Guid>`. `FilterApprovers` in the base class also takes `HashSet<Guid>` (mutable) but only reads it — should accept `IReadOnlySet<Guid>`.

---

### WF-4 · `managersByNodeId` fetch and `BatchFetchRelatedDataAsync` run sequentially

**File:** `WorkflowResolutionService.cs` — `LoadContextAsync`  

```csharp
var managersByNodeId = await _unitOfWork.OrgNodeAssignments.GetManagersByNodesAsync(...); // waits
var (...) = await BatchFetchRelatedDataAsync(...);                                          // then this
```

Neither call depends on the other's result. They can run concurrently with `Task.WhenAll`.

---

### WF-5 · `OrgNode` name always fetched via a live DB call inside the resolver

**File:** `OrgNodeStepResolver.cs`  

`_getNodeById(step.OrgNodeId.Value, ct)` fires a DB round trip per OrgNode step just to retrieve the node's display name. The OrgNode IDs are already known at batch-fetch time. A pre-loaded `OrgNodesById` dictionary in `WorkflowResolutionContext` would eliminate this.

---

### WF-6 · Redundant `SeenApproverIds.Contains` check in `DirectEmployeeStepResolver`

**File:** `DirectEmployeeStepResolver.cs`  

```csharp
if (state.SeenApproverIds.Contains(employee.Id))   // ← checked here
    return Result.Success(...);

var approvers = FilterApprovers(..., state.SeenApproverIds); // ← also checked here
```

The first check is entirely covered by `FilterApprovers`. Can be removed.

---

### WF-7 · Unknown `WorkflowStepType` throws `ArgumentException` instead of returning `Result.Failure`

**File:** `WorkflowStepResolverFactory.cs`  

`Get(type)` throws `ArgumentException` for an unregistered step type. This propagates as an unhandled exception rather than a controlled `Result.Failure` with a meaningful error code. Should return `Result.Failure(DomainErrors.Request.UnknownStepType)` or similar.

---

### WF-8 · Unused `using` in `WorkflowStepResolverBase`

**File:** `WorkflowStepResolverBase.cs`  

`using HrSystemApp.Application.Common.Logging;` is imported but nothing from that namespace is referenced in the base class.

---

### WF-9 · `HasResolver` is dead code on `WorkflowStepResolverFactory`

**File:** `WorkflowStepResolverFactory.cs`  

`public bool HasResolver(WorkflowStepType type)` is never called. Either add a use or remove it.

---

## Priority Summary Table

| ID | Severity | Layer | File / Area | Description | Status |
|---|---|---|---|---|---|
| C-1 | 🔴 Critical | Application | `GetAttendanceSessionsQuery` | Ownership check compares `EmployeeId` to `UserId` — always false | Not fixed |
| C-2 | 🔴 Critical | Infrastructure | `Repository.GetByIdAsync` | `FindAsync` bypasses global soft-delete filter — systemic | ✅ Fixed |
| C-3 | 🔴 Critical | API | `RequestsController` | `admin/company-wide` has no role guard | Not fixed |
| C-4 | 🔴 Critical | Application | `GetRequestByIdQuery` | `isHrOrAbove` has no company scoping — cross-tenant leak | ✅ Fixed |
| C-5 | 🔴 Critical | Application | `OverrideClockIn/Out` | Target employee not validated to caller's company | ✅ Fixed |
| H-1 | 🟠 High | Application | 4 query handlers | All matching rows loaded to memory, paginated in-process | ✅ Fixed |
| H-2 | 🟠 High | Application | `CreateRequestCommand` | N+1 DB calls in step validation loop | ✅ Fixed |
| H-3 | 🟠 High | Application | `OverrideClockInCommand` | Wrong error code returned for future clock-in timestamp | Not fixed |
| M-1 | 🟡 Medium | Application | 3 query handlers | No `PageSize` upper bound | Not fixed |
| M-2 | 🟡 Medium | Infrastructure | `DependencyInjection` | Deprecated `WorkflowService` still registered in DI | Not fixed |
| M-3 | 🟡 Medium | Application | Clock commands | Multiple `SaveChangesAsync` per transaction | Not fixed |
| M-4 | 🟡 Medium | Application | `GetRequestByIdQuery` | `Data` field typed as `object` — untyped, unvalidated | Not fixed |
| L-1 | 🔵 Low | API | `DependencyInjection` | `RequireHttpsMetadata = false` not environment-gated | Not fixed |
| L-2 | 🔵 Low | API | `Program.cs` | CORS `AllowAll` accepts any origin | Not fixed |
| L-3 | 🔵 Low | Infrastructure | `DependencyInjection` | `TokenService` should be `AddSingleton` | Not fixed |
| L-4 | 🔵 Low | Infrastructure | `WorkflowService` | Dead code — interface, implementation, and DI registration | Not fixed |
| WF-1 | ✅ Fixed | Infrastructure | `WorkflowResolutionService` | `CompanyRole` holders keyed by employee ID instead of role ID | ✅ Fixed |
| WF-2 | ✅ Fixed | Infrastructure | `WorkflowResolutionContext` | `TryAddStep` partial-write on approver collision | ✅ Fixed |
| WF-3 | 🔵 Low | Infrastructure | `WorkflowResolutionContext` | `SeenApproverIds` publicly mutable — bypasses `TryAddStep` invariant | Not fixed |
| WF-4 | 🔵 Low | Infrastructure | `WorkflowResolutionService` | Two independent batch fetches run sequentially | Not fixed |
| WF-5 | 🔵 Low | Infrastructure | `OrgNodeStepResolver` | OrgNode name fetched live in resolver — should be pre-loaded | Not fixed |
| WF-6 | 🔵 Low | Infrastructure | `DirectEmployeeStepResolver` | Redundant `SeenApproverIds.Contains` before `FilterApprovers` | Not fixed |
| WF-7 | 🔵 Low | Infrastructure | `WorkflowStepResolverFactory` | Unknown step type throws instead of returning `Result.Failure` | Not fixed |
| WF-8 | 🔵 Low | Infrastructure | `WorkflowStepResolverBase` | Unused `using` statement | Not fixed |
| WF-9 | 🔵 Low | Infrastructure | `WorkflowStepResolverFactory` | `HasResolver` method is dead code | Not fixed |

---

*Total: 5 Critical (2 fixed) · 3 High (2 fixed) · 4 Medium · 4 Low · 9 Workflow items (2 fixed)*
