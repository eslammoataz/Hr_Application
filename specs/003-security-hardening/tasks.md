# Tasks: Phase 1 - Security Hardening

**Input**: Design documents from `/specs/003-security-hardening/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

## Phase 1: Setup (Shared Infrastructure)

- [ ] T001 [P] Create `HrSystemApp.Domain/Constants/AppClaimTypes.cs` and define constants: `Subject = "sub"`, `Email = "email"`, `Role = "role"`, `CompanyId = "companyId"`, and `EmployeeId = "employeeId"`.
- [ ] T002 [P] Add a `SeedPasswordSettings` key to the root of `HrSystemApp.Api/appsettings.json` (leaving value as an empty string for the user to fill).

## Phase 2: Foundational (Blocking Prerequisites)

- [ ] T003 Search all files in `HrSystemApp.Api` and `HrSystemApp.Infrastructure` for hardcoded claim strings (`"sub"`, `"role"`, `"email"`, `"companyId"`, `"employeeId"`) and replace them with `AppClaimTypes` constants.

---

## Phase 3: User Story 1 - Secure System Initialization (Priority: P1) 🎯 MVP

**Goal**: Eliminate hardcoded passwords and sanitize logs.

**Independent Test**: Verify `SeedData.cs` has no string literals for passwords and `LogTestCredentials` doesn't leak plain text.

### Implementation for User Story 1

- [ ] T004 [US1] Add a private `readonly IConfiguration _configuration` field and constructor injection to the `SeedData` class in `HrSystemApp.Infrastructure/Data/SeedData.cs`.
- [ ] T005 [US1] In `SeedData.cs`, locate `CreateDefaultUserAsync` and `CreateHierarchyUserAsync`. Replace the hardcoded `"Pass@123"` with `_configuration["SeedPasswordSettings"] ?? Guid.NewGuid().ToString()`.
- [ ] T006 [US1] In `SeedData.cs`, modify the `LogTestCredentials` method (around line 530) to remove the `Password` property from the anonymous object passed to `_logger.LogInformation`.

---

## Phase 4: User Story 2 - Strong Password Enforcement (Priority: P1)

**Goal**: Enforce 12-character complex passwords and force resets for non-compliant accounts.

**Independent Test**: Register a user with a weak password (reject) and check `MustChangePassword` flag on legacy accounts.

### Implementation for User Story 2

- [ ] T007 [US2] In `HrSystemApp.Infrastructure/DependencyInjection.cs`, update `IdentityOptions.Password` property configuration:
  - Set `RequiredLength = 12`.
  - Set `RequireDigit`, `RequireUppercase`, `RequireLowercase`, `RequireNonAlphanumeric` to `true`.
  - Remove any existing `.AddIdentity` option overrides that weaken these rules.
- [ ] T008 [US2] Create a `SecurityHardeningService.cs` in `HrSystemApp.Infrastructure/Services` that implements `IHostedService`. In `StartAsync`, execute: `await _context.Users.Where(u => u.Email.EndsWith("@hrsystem.com")).ExecuteUpdateAsync(s => s.SetProperty(u => u.MustChangePassword, true), cancellationToken)`.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [ ] T009 [P] Update `HrSystemApp.Infrastructure/README.md` with instructions on how to set the `SeedPasswordSettings` in environment variables.
- [ ] T010 Final validation: Attempt to register a user via `AuthController` with a password shorter than 12 characters and verify a `400 Bad Request` with Identity error messages.
- [ ] T011 [P] Perform a global search for `"Pass@123"` in the `HrSystemApp.Infrastructure` project to ensure no hardcoded instance remains.

---

## Dependencies & Execution Order

- **Phase 1 & 2** are foundational and block User Story implementation.
- **US1 and US2** can proceed in parallel once constants and config placeholders are ready.
- **T008** should be registered as a singleton hosted service in `DependencyInjection.cs`.
