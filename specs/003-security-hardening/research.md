# Research: Phase 1 - Security Hardening

This document consolidates research on technical choices for the security hardening phase.

## Decision 1: Bulk Password Reset Strategy
- **Decision**: Use `ExecuteUpdateAsync` to set `MustChangePassword = true` for all non-compliant users.
- **Rationale**: High performance for bulk updates, avoiding loading thousands of users into memory.
- **Alternatives considered**: Iterating with `UserManager.UpdateAsync` (too slow, N+1 problem).

## Decision 2: Centralized Claim Management
- **Decision**: Create `AppClaimTypes` in `HrSystemApp.Domain.Constants`.
- **Rationale**: Claims are used in Domain (for scoping/rules), Application (for authorization), and Infrastructure (for Identity), so Domain is the most foundational layer.
- **Alternatives considered**: Application layer (would cause circular dependency if Domain needs it).

## Decision 3: Identity Policy Enforcement
- **Decision**: Update `AddIdentity` configuration in `DependencyInjection.cs` to reflect 12-character minimum and complexity.
- **Rationale**: This is the standard entry point for Identity security configuration in ASP.NET Core.

## Decision 4: Seed Data Hardening
- **Decision**: Sourcing passwords from `IConfiguration` with fallbacks to `Guid.NewGuid().ToString()` for temporary development users if no password is provided.
- **Rationale**: Prevents hardcoding `Pass@123` in the codebase.
