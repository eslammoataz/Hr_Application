# Feature Specification: Constitution Refactor & Cleanup

**Feature Branch**: `001-constitution-refactor`  
**Created**: 2026-04-13  
**Status**: Draft  
**Input**: "based on what i have in my constitution file and how my app is working i want to know what i have bad in my code and what i can do to fix and also i want to make a clean code approach in my code so i want everything to be clean and in a good structure the handlers also take in considerations this data"

---

## User Scenarios & Testing *(mandatory)*

### Developer Story 1 - Remove Dead Code and Duplicates (Priority: P1)

As a developer, I want to remove unused and duplicate code so the codebase is smaller and easier to navigate.

**Why this priority**: Dead code creates confusion about which types are actually in use and can cause naming conflicts.

**Independent Test**: Run `dotnet build` after each task — if it succeeds, the change is safe.

**Acceptance Scenarios**:

1. **Given** `HrSystemApp.Domain/Common/BaseEntity.cs` exists, **When** the file is deleted, **Then** no other file depends on it for compilation.
2. **Given** `RequestConfiguration.cs` exists without a `HasQueryFilter`, **When** the filter is added, **Then** soft-deleted Requests are automatically excluded from queries.

---

### Developer Story 2 - Type Safety for Status Fields (Priority: P1)

As a developer, I want status fields to use strong-typed enums instead of magic strings so refactoring is safer.

**Why this priority**: String comparisons are error-prone and IDE tooling cannot catch typos.

**Independent Test**: `dotnet build` passes with zero warnings about ambiguous enum/string comparisons.

**Acceptance Scenarios**:

1. **Given** `ProfileUpdateRequest.Status` is a `string`, **When** it is changed to `ProfileUpdateRequestStatus` enum, **Then** all handlers that compare or assign status values use the enum type.

---

### Developer Story 3 - Read-Only Query Optimization (Priority: P2)

As a developer, I want read-only queries to use `.AsNoTracking()` so EF Core does not materialize change-tracking proxies unnecessarily.

**Why this priority**: Reduces memory overhead and speeds up read-only operations.

**Independent Test**: `dotnet build` succeeds; no functional behavior change expected.

**Acceptance Scenarios**:

1. **Given** a repository method that only reads data, **When** it is called, **Then** the resulting entities are not tracked by the DbContext.

---

### Developer Story 4 - Reduce SaveChangesAsync Calls (Priority: P2)

As a developer, I want handlers to call `SaveChangesAsync` the minimum number of times so transactions are shorter and performance is better.

**Why this priority**: Each `SaveChangesAsync` round-trips to the database; batching them reduces latency.

**Independent Test**: `dotnet test` passes — the business outcome is unchanged.

**Acceptance Scenarios**:

1. **Given** the ClockIn flow, **When** clock-in completes, **Then** exactly three `SaveChangesAsync` calls occur: one to persist the Attendance record (for its Id), one to persist the AttendanceLog (for its Id), and one final save before commit.
2. **Given** the ClockOut flow, **When** clock-out completes, **Then** exactly three `SaveChangesAsync` calls occur following the same pattern.

---

### Developer Story 5 - Centralized Object Mapping (Priority: P2)

As a developer, I want object mappings defined in Mapster registers so the mapping logic is reusable and testable.

**Why this priority**: Inline `.Select()` projections are duplicated across handlers and become stale when DTOs change.

**Independent Test**: `dotnet test` passes for the attendance query handler.

**Acceptance Scenarios**:

1. **Given** `GetCompanyAttendanceQueryHandler` uses `Adapt<List<AttendanceSummaryResponse>>()`, **When** the query executes, **Then** the result matches the manually-projected output exactly.

---

### Developer Story 6 - Input Validation Coverage (Priority: P2)

As a developer, I want all user-input-carrying commands to have FluentValidation validators so invalid data is rejected before reaching the handler.

**Why this priority**: Validators caught by the MediatR pipeline fail fast and return structured error responses without reaching business logic.

**Independent Test**: `dotnet build` succeeds; validators are discovered by `ValidationBehavior`.

**Acceptance Scenarios**:

1. **Given** a `ClockInCommand` with a future timestamp, **When** it is dispatched, **Then** the `ClockInCommandValidator` returns a validation error.
2. **Given** an `UpdateCompanyCommand` with `GraceMinutes = -5`, **When** it is dispatched, **Then** the `UpdateCompanyCommandValidator` returns a validation error.

---

### Developer Story 7 - Extract Placement Logic into a Service (Priority: P3)

As a developer, I want placement and leadership assignment logic extracted from `CreateEmployeeCommandHandler` into a dedicated service so the handler stays under 60 lines.

**Why this priority**: Large handlers are hard to test and review; placing related logic in a service makes it reusable.

**Independent Test**: `dotnet test` passes for the employee creation flow.

**Acceptance Scenarios**:

1. **Given** `CreateEmployeeCommandHandler` references `IEmployeePlacementService`, **When** an employee is created with a leadership role, **Then** the leadership demotion logic is applied via the service.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST delete `HrSystemApp.Domain/Common/BaseEntity.cs` — it duplicates `HrSystemApp.Domain/Models/BaseEntity.cs` and is unused.
- **FR-002**: System MUST add `HasQueryFilter(x => !x.IsDeleted)` to `RequestConfiguration.cs` so soft-deleted Requests are filtered from all queries.
- **FR-003**: System MUST change `ProfileUpdateRequest.Status` from `string` to `ProfileUpdateRequestStatus` enum using `.HasConversion<string>()` in EF config.
- **FR-004**: System MUST delete `EmployeeRepository.GetPagedAsync` (returning `PagedResult<Employee>`) — it is unused and flagged for query optimization.
- **FR-005**: System MUST add `.AsNoTracking()` to read-only repository methods: `DepartmentRepository.GetWithUnitsAsync`, `DepartmentRepository.GetByCompanyAsync`, `RequestDefinitionRepository.GetByTypeAsync`, `RequestDefinitionRepository.GetByCompanyAsync`, `RequestRepository.GetByEmployeeIdAsync`, `RequestRepository.GetPendingApprovalsAsync`, `RefreshTokenRepository.GetActiveTokensByUserIdAsync`, `CompanyRepository.GetWithDetailsAsync`, `CompanyRepository.GetPagedAsync`, `UserRepository.GetByEmailWithDetailsAsync`, `TeamRepository.GetWithMembersAsync`.
- **FR-006**: System MUST refactor `ClockInCommandHandler` and `ClockOutCommandHandler` to call `SaveChangesAsync` exactly three times per flow (Attendance persist, AttendanceLog persist, final commit batch).
- **FR-007**: System MUST create `AttendanceMappingRegister.cs` in `HrSystemApp.Application/Mappings/` and use `Adapt<List<AttendanceSummaryResponse>>()` in `GetCompanyAttendanceQueryHandler`.
- **FR-008**: System MUST create FluentValidation validators for `ClockInCommand`, `ClockOutCommand`, `ChangeEmployeeStatusCommand`, `UpdateCompanyCommand`, `UpdateMyCompanyCommand`, and `ConfigureHierarchyPositionsCommand`.
- **FR-009**: System MUST extract `ResolvePlacementAsync`, `AssignLeadershipIfNeededAsync`, and `DemoteOldLeaderIfNeededAsync` from `CreateEmployeeCommandHandler` into `IEmployeePlacementService` implementation.

---

## Key Entities *(include if feature involves data)*

- **BaseEntity** (in `Domain/Models/`): Keeps `Id`, `IsDeleted`, `CreatedAt`, `UpdatedAt` — the one in `Domain/Common/` must be deleted.
- **Request**: Entity needing soft-delete query filter in EF configuration.
- **ProfileUpdateRequest**: Entity whose `Status` string field should become an enum.
- **Attendance, AttendanceLog**: Entities involved in ClockIn/ClockOut transaction refactor.
- **EmployeePlacementService**: New service to hold placement resolution and leadership assignment logic.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `dotnet build` completes with zero errors and zero warnings about ambiguous types.
- **SC-002**: `dotnet test` passes with all existing tests succeeding.
- **SC-003**: `CreateEmployeeCommandHandler.Handle()` contains under 60 lines after extraction.
- **SC-004**: All 9 tasks from the HRMS Fix Spec are completed with no additions or omissions.
- **SC-005**: No regression in functionality — soft-delete, enum status, and transaction behavior remain functionally equivalent.

---

## Assumptions

- EF Core migrations are out of scope — enum conversion uses `.HasConversion<string>()` so existing string data in the database is not broken.
- Test files are not modified — if a test breaks due to a type change, it is fixed to use the enum value.
- No public API endpoints, DTOs, or command record properties are renamed.
