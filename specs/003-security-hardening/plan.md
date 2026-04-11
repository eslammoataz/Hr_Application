# Implementation Plan: Phase 1 - Security Hardening

This plan details the technical implementation of security hardening for the HR System App.

## Technical Context

### Architecture & Design
- **Layer**: Infrastructure (for Identity logic) and Domain (for constants).
- **Patterns**: Bulk update via `ExecuteUpdateAsync` to minimize performance impact.

### Unknowns
- [Resolved] Strategy for existing non-compliant passwords: Force `MustChangePassword = true`.

## Constitution Check

| Principle | Status | Note |
|-----------|--------|------|
| I. Clean Architecture | ✅ | Constants placed in Domain; configuration in Infrastructure. |
| V. Security & Privacy | ✅ | Eliminating hardcoded secrets and hardening password policies. |

## Proposed Changes

### 🛡️ Security & Identity

#### [MODIFY] [DependencyInjection.cs](file:///e:/Github%20Repos/HR%20System%20App/HrSystemApp/HrSystemApp.Infrastructure/DependencyInjection.cs)
- Update `AddIdentity` configuration:
  - `RequiredLength = 12`
  - `RequireDigit = true`
  - `RequireUppercase = true`
  - `RequireLowercase = true`
  - `RequireNonAlphanumeric = true`
- Remove weak password overrides.

#### [NEW] [AppClaimTypes.cs](file:///e:/Github%20Repos/HR%20System%20App/HrSystemApp/HrSystemApp.Domain/Constants/AppClaimTypes.cs)
- Create constants for all JWT claim types used in the application.

#### [MODIFY] [SeedData.cs](file:///e:/Github%20Repos/HR%20System%20App/HrSystemApp/HrSystemApp.Infrastructure/Data/SeedData.cs)
- Replace all hardcoded passwords with `configuration.GetSection("SeedPasswordSettings")` calls.
- Sanitize `LogTestCredentials` to remove plain-text passwords.

#### [NEW] [SecurityHardeningService.cs](file:///e:/Github%20Repos/HR%20System%20App/HrSystemApp/HrSystemApp.Infrastructure/Services/SecurityHardeningService.cs)
- Implement a one-off or startup task to run `context.Users.Where(u => u.PasswordHash != null && u.PasswordHash.Length < 100).ExecuteUpdateAsync(...)` or similar logic to flag non-compliant users. Note: Identity hashes vary, so we'll likely use a logic check or just reset all non-admin users if requested. 
- *Clarified*: We will target all users seeded with legacy passwords.

## Verification Plan

### Automated Tests
- Integration test to verify that a login with a weak password fails during registration and change password.
- Integration test to verify that a newly seeded user (with hardened seeding) can log in securely.

### Manual Verification
- Attempt to register a new user with "Pass@123" and ensure it is rejected.
- Check identity database to ensure `MustChangePassword` is correctly set for legacy accounts.
