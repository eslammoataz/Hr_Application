# Feature Specification: Phase 1 - Security Hardening

**Feature Branch**: `003-security-hardening`  
**Created**: 2026-04-11  
**Status**: Draft  
**Input**: Full Project Audit results.

## Clarifications

### Session 2026-04-11
- Q: When we harden the Identity password rules, how should we handle existing users? → A: Force `MustChangePassword = true` for all users whose current passwords do not meet the new 12-character/complexity requirements to ensure immediate compliance.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Secure System Initialization (Priority: P1)

As a system administrator, I want the system to be initialized without any hardcoded passwords or secrets in the source code, so that my environment is secure from credential theft via version control.

**Why this priority**: Protecting credentials is the highest priority for system integrity.

**Independent Test**: Can be tested by inspecting `SeedData.cs` and verifying that no passwords exist as string literals, and that the system still seeds accounts correctly using environment variables.

**Acceptance Scenarios**:
1. **Given** the system is being seeded, **When** accounts are created, **Then** passwords must be sourced from secure configuration (settings/env) or generated randomly.
2. **Given** the seeding process, **When** it completes, **Then** no plain-text passwords should be emitted to logs.

---

### User Story 2 - Strong Password Enforcement (Priority: P1)

As a security-conscious user, I want the system to force me to choose a complex password, so that my account is protected from brute-force attacks.

**Why this priority**: Defending against common automated attacks.

**Independent Test**: Can be tested by attempting to register or change a password to "123456" and verifying it is rejected by the system.

**Acceptance Scenarios**:
1. **Given** a password change request, **When** the password does not contain a digit, uppercase letter, or special character, **Then** the request is rejected with a validation error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-SEC-001**: System MUST NOT store passwords, tokens, or encryption keys in source code files.
- **FR-SEC-002**: System MUST load administrative credentials from `SuperAdminSettings` and `SeedPasswordSettings` configuration sections.
- **FR-SEC-003**: Identity password requirements MUST be updated to:
  - Minimum length: 12 characters.
  - Require digit: Yes.
  - Require uppercase: Yes.
  - Require lowercase: Yes.
  - Require non-alphanumeric (special character): Yes.
- **FR-SEC-004**: Claims literacy MUST be centralized in a `ClaimTypesConstants` class (or similar) to avoid hardcoded `"sub"`, `"role"`, etc.
- **FR-SEC-005**: `SeedData.LogTestCredentials` MUST BE removed or sanitized to prevent leaking passwords in logs.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero occurrences of hardcoded password literals in `HrSystemApp.Infrastructure`.
- **SC-002**: 100% of claim lookups use centralized constants.
- **SC-003**: Security scanners (e.g., GitHub Advanced Security) report zero "hardcoded secrets" in the codebase.
- **SC-004**: Existing accounts with passwords that do not meet the new 12-character and complexity requirements will be flagged for an immediate password change (`MustChangePassword = true`).

## Assumptions

- Admin credentials for seeding will be provided via `appsettings.json` (for local dev) and environment variables (for staging/prod).
- Existing seeded users' passwords may need to be reset if they don't meet new requirements, but this is acceptable for a development-ready state.
