# Tasks: Constitution Refactor & Cleanup

**Input**: Design documents from `specs/001-constitution-refactor/`
**Prerequisites**: plan.md (complete), spec.md (complete)
**Tests**: No new tests required — this is a refactor-only task list

**Organization**: Tasks are grouped into two phases: Code Cleanup (remove dead code, add type safety) and Optimization & Validation (query optimization, transaction refactor, mapping, validators, service extraction). Each phase is independent; all phases can run in parallel with no cross-phase dependencies. Within each phase, tasks that touch different files can run in parallel.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Not applicable — developer-only refactor with no user stories
- Include exact file paths in descriptions

---

## Phase 1: Code Cleanup

**Purpose**: Remove dead/duplicate code and add type safety.

### T001 [P] [US1] Delete duplicate `BaseEntity` in `Domain/Common`

**File**: `HrSystemApp.Domain/Common/BaseEntity.cs` → DELETE ENTIRE FILE

**Also fix**: `HrSystemApp.Infrastructure/Data/ApplicationDbContext.cs`
- Remove `using HrSystemApp.Domain.Common;` from line 1 (it is unused; line 6 already has `using BaseEntity = HrSystemApp.Domain.Models.BaseEntity;`)
- The alias reference to `HrSystemApp.Domain.Models.BaseEntity` must remain

**Verification**: `dotnet build HrSystemApp.sln` — zero errors

---

### T002 [P] [US2] Remove dead `GetPagedAsync` from `EmployeeRepository`

**File**: `HrSystemApp.Infrastructure/Repositories/EmployeeRepository.cs`
- DELETE the entire `GetPagedAsync` method (lines ~64–101) — the method returning `PagedResult<Employee>` that uses `.Include()` and has a comment `// for me "needs some query optimization"`
- No callers exist — confirmed by exhaustive search

**Interface**: `HrSystemApp.Infrastructure/Repositories/IEmployeeRepository.cs`
- DELETE the `Task<PagedResult<Employee>> GetPagedAsync(...)` declaration from line ~15–16

**Verification**: `dotnet build HrSystemApp.sln` — zero errors

---

### T003 [P] [US3] Replace magic string `Status` with enum on `ProfileUpdateRequest`

**Files** (4 files, all can run in parallel since they are edits to different files):

**File 1** — `HrSystemApp.Domain/Models/ProfileUpdateRequest.cs`
- Add `using HrSystemApp.Domain.Enums;` at top
- Change `public string Status { get; set; } = "Pending";` to `public ProfileUpdateRequestStatus Status { get; set; } = ProfileUpdateRequestStatus.Pending;`

**File 2** — `HrSystemApp.Application/Features/ProfileUpdateRequests/Commands/CreateProfileUpdateRequest/CreateProfileUpdateRequestCommandHandler.cs`
- Replace `r.Status == "Pending"` with `r.Status == ProfileUpdateRequestStatus.Pending`
- Replace `Status = "Pending"` with `Status = ProfileUpdateRequestStatus.Pending`

**File 3** — `HrSystemApp.Application/Features/ProfileUpdateRequests/Commands/HandleProfileUpdateRequest/HandleProfileUpdateRequestCommandHandler.cs`
- Replace `if (request.Status != "Pending")` with `if (request.Status != ProfileUpdateRequestStatus.Pending)`
- Replace `request.Status = command.Dto.IsAccepted ? "Approved" : "Rejected"` with `request.Status = command.Dto.IsAccepted ? ProfileUpdateRequestStatus.Approved : ProfileUpdateRequestStatus.Rejected`

**File 4** — `HrSystemApp.Infrastructure/Repositories/ProfileUpdateRequestRepository.cs`
- Remove `var statusString = status.Value.ToString();` and change `query = query.Where(r => r.Status == statusString);` to `query = query.Where(r => r.Status == status.Value);`

**EF Config** (if exists) — `HrSystemApp.Infrastructure/Data/Configurations/ProfileUpdateRequestConfiguration.cs`
- If `Status` is configured, ensure it has `.HasConversion<string>()`
- If no configuration exists for `ProfileUpdateRequest`, this step is skipped

**Verification**: `dotnet build HrSystemApp.sln` — zero errors

---

### T004 [P] [US4] Add `HasQueryFilter` for soft-delete on `Request`

**File**: `HrSystemApp.Infrastructure/Data/Configurations/RequestConfiguration.cs`
- Add `builder.HasQueryFilter(x => !x.IsDeleted);` immediately after `builder.HasKey(x => x.Id);`

**Verification**: `dotnet build HrSystemApp.sln` — zero errors

---

## Phase 2: Query Optimization & Transaction Refactor

**Purpose**: Optimize read-only queries and reduce unnecessary database round-trips.

### T005 [P] [US5] Add `.AsNoTracking()` to read-only repository methods

**File 1** — `HrSystemApp.Infrastructure/Repositories/DepartmentRepository.cs`
- `GetWithUnitsAsync`: change `_context.Departments` → `_context.Departments.AsNoTracking()` (before `.Include()`)
- `GetByCompanyAsync`: change `_context.Departments` → `_context.Departments.AsNoTracking()` (before `.Include()`)

**File 2** — `HrSystemApp.Infrastructure/Repositories/RequestDefinitionRepository.cs`
- `GetByTypeAsync`: change `_dbSet` → `_dbSet.AsNoTracking()` (before `.Include()`)
- `GetByCompanyAsync`: change `_dbSet` → `_dbSet.AsNoTracking()` (before `.Where()`)

**File 3** — `HrSystemApp.Infrastructure/Repositories/RequestRepository.cs`
- `GetByEmployeeIdAsync`: change `_dbSet` → `_dbSet.AsNoTracking()` (before `.Where()`)
- `GetPendingApprovalsAsync`: change `_dbSet` → `_dbSet.AsNoTracking()` (before `.Where()`)
- `GetByIdWithHistoryAsync`: **DO NOT add AsNoTracking** — `ApproveRequestCommandHandler` mutates the loaded entity

**File 4** — `HrSystemApp.Infrastructure/Repositories/RefreshTokenRepository.cs`
- `GetActiveTokensByUserIdAsync`: change `_dbSet` → `_dbSet.AsNoTracking()` (before `.Where()`)
- `GetByTokenHashAsync`: **DO NOT add AsNoTracking** — `RevokeAllTokensForUserAsync` mutates after load

**File 5** — `HrSystemApp.Infrastructure/Repositories/CompanyRepository.cs`
- `GetWithDetailsAsync`: change `_dbSet.AsQueryable()` → `_dbSet.AsNoTracking()` (before any `.Include()`)
- `GetPagedAsync`: change `_dbSet.AsQueryable()` → `_dbSet.AsNoTracking()` (before any conditional `.Include()`)

**File 6** — `HrSystemApp.Infrastructure/Repositories/UserRepository.cs`
- `GetByEmailWithDetailsAsync`: change `_dbSet` → `_dbSet.AsNoTracking()` (before `.Include()`)

**File 7** — `HrSystemApp.Infrastructure/Repositories/TeamRepository.cs`
- `GetWithMembersAsync`: change `_context.Teams` → `_context.Teams.AsNoTracking()` (before `.Include()`)

**Verification**: `dotnet build HrSystemApp.sln` — zero errors

---

### T006 [P] [US6] Collapse `SaveChangesAsync` calls in ClockIn and ClockOut handlers

**File 1** — `HrSystemApp.Application/Features/Attendance/Commands/ClockIn/ClockInCommand.cs`
- Read the full `try` block (~lines 57–108)
- Current flow: `SaveChangesAsync` after attendance insert, `SaveChangesAsync` after log insert, `SaveChangesAsync` after attendance update, then `CommitTransactionAsync`
- Remove the `SaveChangesAsync` after `Attendances.UpdateAsync` — the final `SaveChangesAsync` before `CommitTransactionAsync` covers the update
- Keep: (1) Attendance insert save, (2) Log insert save (needed for FK), (3) Final save before commit
- Result: exactly 3 `SaveChangesAsync` calls total

**File 2** — `HrSystemApp.Application/Features/Attendance/Commands/ClockOut/ClockOutCommand.cs`
- Read the full `try` block (~lines 65–104)
- Current flow: `SaveChangesAsync` after log insert, `SaveChangesAsync` after attendance update, then `CommitTransactionAsync`
- This is already minimal — keep both `SaveChangesAsync` calls (log FK + final save)
- No change needed — document that 2 calls is the minimum for this flow

**Verification**: `dotnet test` — all tests pass

---

## Phase 3: Mapping, Validation & Service Extraction

**Purpose**: Centralize mapping logic, add missing validators, extract large handler methods into a reusable service.

### T007 [P] [US7] Replace inline `.Select()` with Mapster in attendance query

**CREATE** — `HrSystemApp.Application/Mappings/AttendanceMappingRegister.cs`
```csharp
using HrSystemApp.Application.DTOs.Attendance;
using HrSystemApp.Domain.Models;
using Mapster;

namespace HrSystemApp.Application.Mappings;

public class AttendanceMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Attendance, AttendanceSummaryResponse>()
            .Map(dest => dest.EmployeeId, src => src.EmployeeId)
            .Map(dest => dest.EmployeeName, src => src.Employee != null ? src.Employee.FullName : string.Empty)
            .Map(dest => dest.Date, src => src.Date)
            .Map(dest => dest.FirstClockInUtc, src => src.FirstClockInUtc)
            .Map(dest => dest.LastClockOutUtc, src => src.LastClockOutUtc)
            .Map(dest => dest.TotalHours, src => src.TotalHours)
            .Map(dest => dest.Status, src => src.Status.ToString())
            .Map(dest => dest.IsLate, src => src.IsLate)
            .Map(dest => dest.IsEarlyLeave, src => src.IsEarlyLeave)
            .Map(dest => dest.Reason, src => src.Reason);
    }
}
```

**UPDATE** — `HrSystemApp.Application/Features/Attendance/Queries/GetCompanyAttendance/GetCompanyAttendanceQuery.cs`
- Find the `.Select()` block inside `GetCompanyAttendanceQueryHandler` that manually constructs `AttendanceSummaryResponse`
- Replace with `paged.Items.Adapt<List<AttendanceSummaryResponse>>()`
- The Mapster register is auto-discovered via `mapsterConfig.Scan(assembly)` in `DependencyInjection.cs`

**Verification**: `dotnet build` — zero errors; `dotnet test` — pass

---

### T008 [P] [US8] Add 6 FluentValidation validators

All files below use the namespace pattern `HrSystemApp.Application.Features.<Feature>.<Command|Query>` and inherit from `AbstractValidator<T>` where T is the command being validated. All are auto-discovered by `AddValidatorsFromAssembly(assembly)` already registered in `DependencyInjection.cs`.

**CREATE 1** — `HrSystemApp.Application/Features/Attendance/Commands/ClockIn/ClockInCommandValidator.cs`
```csharp
using FluentValidation;

namespace HrSystemApp.Application.Features.Attendance.Commands.ClockIn;

public class ClockInCommandValidator : AbstractValidator<ClockInCommand>
{
    public ClockInCommandValidator()
    {
        RuleFor(x => x.TimestampUtc)
            .Must(ts => ts == null || ts.Value <= DateTime.UtcNow.AddMinutes(5))
            .WithMessage("Clock-in timestamp cannot be in the future.");
    }
}
```

**CREATE 2** — `HrSystemApp.Application/Features/Attendance/Commands/ClockOut/ClockOutCommandValidator.cs`
```csharp
using FluentValidation;

namespace HrSystemApp.Application.Features.Attendance.Commands.ClockOut;

public class ClockOutCommandValidator : AbstractValidator<ClockOutCommand>
{
    public ClockOutCommandValidator()
    {
        RuleFor(x => x.TimestampUtc)
            .Must(ts => ts == null || ts.Value <= DateTime.UtcNow.AddMinutes(5))
            .WithMessage("Clock-out timestamp cannot be in the future.");
    }
}
```

**CREATE 3** — `HrSystemApp.Application/Features/Employees/Commands/ChangeEmployeeStatus/ChangeEmployeeStatusCommandValidator.cs`
```csharp
using FluentValidation;

namespace HrSystemApp.Application.Features.Employees.Commands.ChangeEmployeeStatus;

public class ChangeEmployeeStatusCommandValidator : AbstractValidator<ChangeEmployeeStatusCommand>
{
    public ChangeEmployeeStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Employee ID is required.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid employment status value.");
    }
}
```

**CREATE 4** — `HrSystemApp.Application/Features/Companies/Commands/UpdateCompany/UpdateCompanyCommandValidator.cs`
```csharp
using FluentValidation;

namespace HrSystemApp.Application.Features.Companies.Commands.UpdateCompany;

public class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Company ID is required.");

        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(300).WithMessage("Company name must not exceed 300 characters.");

        RuleFor(x => x.GraceMinutes)
            .GreaterThanOrEqualTo(0).WithMessage("Grace minutes cannot be negative.")
            .LessThanOrEqualTo(120).WithMessage("Grace minutes cannot exceed 120.");

        RuleFor(x => x.YearlyVacationDays)
            .GreaterThan(0).WithMessage("Yearly vacation days must be greater than 0.")
            .LessThanOrEqualTo(365).WithMessage("Yearly vacation days cannot exceed 365.");

        RuleFor(x => x.TimeZoneId)
            .NotEmpty().WithMessage("Time zone is required.")
            .MaximumLength(100);

        RuleFor(x => x)
            .Must(x => x.StartTime < x.EndTime)
            .WithMessage("Start time must be before end time.");
    }
}
```

**CREATE 5** — `HrSystemApp.Application/Features/Companies/Commands/UpdateMyCompany/UpdateMyCompanyCommandValidator.cs`
```csharp
using FluentValidation;

namespace HrSystemApp.Application.Features.Companies.Commands.UpdateMyCompany;

public class UpdateMyCompanyCommandValidator : AbstractValidator<UpdateMyCompanyCommand>
{
    public UpdateMyCompanyCommandValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(300).WithMessage("Company name must not exceed 300 characters.");

        RuleFor(x => x.GraceMinutes)
            .GreaterThanOrEqualTo(0).WithMessage("Grace minutes cannot be negative.")
            .LessThanOrEqualTo(120).WithMessage("Grace minutes cannot exceed 120.");

        RuleFor(x => x.YearlyVacationDays)
            .GreaterThan(0).WithMessage("Yearly vacation days must be greater than 0.")
            .LessThanOrEqualTo(365).WithMessage("Yearly vacation days cannot exceed 365.");

        RuleFor(x => x.TimeZoneId)
            .NotEmpty().WithMessage("Time zone is required.")
            .MaximumLength(100);

        RuleFor(x => x)
            .Must(x => x.StartTime < x.EndTime)
            .WithMessage("Start time must be before end time.");
    }
}
```

**CREATE 6** — `HrSystemApp.Application/Features/Hierarchy/Commands/ConfigureHierarchyPositions/ConfigureHierarchyPositionsCommandValidator.cs`
```csharp
using FluentValidation;

namespace HrSystemApp.Application.Features.Hierarchy.Commands.ConfigureHierarchyPositions;

public class ConfigureHierarchyPositionsCommandValidator : AbstractValidator<ConfigureHierarchyPositionsCommand>
{
    public ConfigureHierarchyPositionsCommandValidator()
    {
        RuleFor(x => x.Positions)
            .NotEmpty().WithMessage("At least one hierarchy position is required.")
            .Must(positions => positions.Select(p => p.SortOrder).Distinct().Count() == positions.Count)
            .WithMessage("Duplicate sort orders are not allowed.")
            .Must(positions => positions.Select(p => p.Role).Distinct().Count() == positions.Count)
            .WithMessage("Duplicate roles are not allowed.");

        RuleForEach(x => x.Positions).ChildRules(position =>
        {
            position.RuleFor(p => p.PositionTitle)
                .NotEmpty().WithMessage("Position title is required.")
                .MaximumLength(200);

            position.RuleFor(p => p.SortOrder)
                .GreaterThan(0).WithMessage("Sort order must be greater than 0.");
        });
    }
}
```

**Verification**: `dotnet build` — zero errors

---

### T009 [US9] Extract `CreateEmployeeCommandHandler` placement logic into `IEmployeePlacementService`

**Step 1 — CREATE interface** — `HrSystemApp.Application/Interfaces/Services/IEmployeePlacementService.cs`
```csharp
using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Services;

public interface IEmployeePlacementService
{
    Task<Result<(Guid? DepartmentId, Guid? UnitId, Guid? TeamId)>> ResolvePlacementAsync(
        Guid companyId,
        Guid? departmentId,
        Guid? unitId,
        Guid? teamId,
        CancellationToken cancellationToken);

    Task<Result> AssignLeadershipIfNeededAsync(
        Employee employee,
        UserRole role,
        CancellationToken cancellationToken);
}
```

**Step 2 — CREATE service** — `HrSystemApp.Application/Services/EmployeePlacementService.cs`
- Copy `ResolvePlacementAsync` from `CreateEmployeeCommandHandler` — make it public, adjust signature to accept individual `Guid` params instead of the command object
- Copy `AssignLeadershipIfNeededAsync` from `CreateEmployeeCommandHandler` — make it public
- Copy `DemoteOldLeaderIfNeededAsync` from `CreateEmployeeCommandHandler` — keep private
- Inject `IUnitOfWork` via constructor

**Step 3 — UPDATE handler** — `HrSystemApp.Application/Features/Employees/Commands/CreateEmployee/CreateEmployeeCommandHandler.cs`
- Inject `IEmployeePlacementService` instead of containing the logic inline
- Replace internal calls to `ResolvePlacementAsync(request, ...)` with `_placementService.ResolvePlacementAsync(companyId, deptId, unitId, teamId, cancellationToken)`
- Replace internal calls to `AssignLeadershipIfNeededAsync(employee, role, ...)` with `_placementService.AssignLeadershipIfNeededAsync(employee, role, cancellationToken)`
- After extraction, `Handle()` method should be under 60 lines

**Step 4 — REGISTER service** — `HrSystemApp.Application/DependencyInjection.cs`
- Add `services.AddScoped<IEmployeePlacementService, EmployeePlacementService>();` in the `AddApplication` method

**Verification**: `dotnet build` — zero errors; `dotnet test` — pass

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Code Cleanup)**: No dependencies — can start immediately
  - T001, T002, T003, T004 are all independent and can run in parallel
- **Phase 2 (Optimization)**: No dependencies on Phase 1 — can run in parallel with Phase 1
  - T005, T006 are independent of Phase 1 tasks
- **Phase 3 (Mapping, Validation, Extraction)**: No dependencies on Phase 1 or 2 — can run in parallel
  - T007, T008 are independent of all others
  - T009 is independent but do LAST (largest extraction)

### Within Each Phase

- Phase 1: T001–T004 are all parallelizable (different files, no dependencies)
- Phase 2: T005 and T006 touch different files — parallelizable
- Phase 3: T007 and T008 create new files — parallelizable; T009 depends on nothing but should run last

### Parallel Execution Example

```bash
# Phase 1 — all 4 tasks in parallel:
T001 (delete BaseEntity)
T002 (remove GetPagedAsync)
T003 (enum conversion)
T004 (Request HasQueryFilter)

# Phase 2 — both tasks in parallel:
T005 (AsNoTracking on 7 files)
T006 (SaveChangesAsync collapse)

# Phase 3 — T007 and T008 in parallel, T009 last:
T007 (Mapster register)
T008 (6 validators)
T009 (placement service extraction)
```

---

## Implementation Strategy

### All Phases Together (recommended for small team)

1. Complete Phase 1 (all 4 tasks) — build to verify
2. Complete Phase 2 (both tasks) — build to verify
3. Complete Phase 3 (all 3 tasks) — build and test to verify
4. Run full `dotnet test` — confirm all pass

### Incremental Delivery

Since this is a refactor-only feature, "MVP" is simply all 9 tasks complete. There is no user-facing value until all are done — but each task can be built and verified independently.

---

## Notes

- No new EF migrations needed — enum conversion uses `.HasConversion<string>()` which maps enum to existing string values in the DB
- No test files should be changed — if a test breaks due to `ProfileUpdateRequest.Status` becoming an enum, fix the test to use `ProfileUpdateRequestStatus.Pending`
- No public API endpoints, DTOs, or command record properties are renamed
- No new `contracts/` directory needed — no external interfaces added
