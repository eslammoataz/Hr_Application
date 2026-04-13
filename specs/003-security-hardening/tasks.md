# Tasks: Phase 3 - Architectural Cleanup & Hardening

**Input**: implementation_plan.md (Phase 3)
**Prerequisites**: ApplicationDbContext, DependencyInjection, AppClaimTypes

## Phase 1: Foundational (Soft-Delete Automation)

**Purpose**: Move from manual soft-delete filtering to automated global query filters.

- [x] T024 [P] [US1] Implement `ApplyGlobalFilters` extension or method in `HrSystemApp.Infrastructure/Data/ApplicationDbContext.cs`
- [x] T025 [US1] Apply the query filter to all `BaseEntity` types in `OnModelCreating` (ApplicationDbContext.cs)
- [x] T026 [US1] Remove redundant `!e.IsDeleted` checks in `HrSystemApp.Infrastructure/Repositories/EmployeeRepository.cs`
- [x] T027 [US1] Verify that `GetWithDetailsAsync` still works correctly after removing manual filters

---

## Phase 2: User Story 2 - Security & DI Sanitization (Priority: P2)

**Goal**: Clean up the service container and standardize claim lookups.

- [x] T028 [P] [US2] Remove duplicate `IWorkflowService` and `IHierarchyService` registrations in `HrSystemApp.Infrastructure/DependencyInjection.cs`
- [x] T029 [P] [US2] Replace hardcoded `"sub"` and `"role"` literals in `HrSystemApp.Infrastructure/Services/TokenService.cs` with `AppClaimTypes`
- [x] T030 [P] [US2] Standardize claim lookups in `HrSystemApp.Api/Controllers/EmployeesController.cs` (lines 58, 170, 184)
- [x] T031 [P] [US2] Replace `"sub"` in `tests/HrSystemApp.Tests.Integration/Infrastructure/JwtTokenFactory.cs` (if applicable)

---

## Phase 3: User Story 3 - Asynchronous Resilience (Priority: P3)

**Goal**: Ensure all async operations are cancellable and robust.

- [x] T032 [P] [US3] Add `CancellationToken` parameter to `GetMyBalances` in `EmployeesController.cs`
- [x] T033 [US3] Propagate `cancellationToken` to `_sender.Send(...)` in `EmployeesController.cs`
- [x] T034 [US3] Audit `Application` layer handlers for any missing `cancellationToken` usage in external calls (e.g., MinioService, EmailService)

---

## Phase 4: Polish & Verification

**Purpose**: Final validation of the architectural hardening.

- [/] T035 Perform a full project build to ensure no regression in DI or constants
- [ ] T036 Run integration tests to verify global filters correctly hide soft-deleted records
- [ ] T037 Verify that `GetCompanyHierarchy` still operates efficiently with global filters active

---

## Dependencies & Execution Order

1. **Foundational (Phase 1)**: Can start after Phase 2 (Hierarchy) is complete.
2. **Story 2 & 3**: Can run in parallel with each other but after Phase 1 is verified for core data stability.
3. **Polish**: Final step after all story tasks are marked complete.

## Implementation Strategy

1. **Global Filters First**: This is the most structural change. Breaking this first ensures we don't build on top of redundant manual filters.
2. **Sanitization**: Cleaning up DI and Claims is low-risk but high-value for code quality.
3. **Async**: Ensures the system is ready for production-level concurrency.
