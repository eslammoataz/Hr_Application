# Data Model: Phase 1 - Security Hardening

This document outlines changes to the data constraints and entities for security hardening.

## [MODIFY] ApplicationUser
- **Constraint Update**: Password requirements changed from weak (length 6, no complexity) to strong (length 12, complexity required).
- **New Behavior**: `MustChangePassword` flag will be utilized for existing non-compliant accounts.

## [NEW] Claim Types
- **Constants**: `AppClaimTypes` class in the Domain layer to hold canonical claim strings (`sub`, `employeeId`, etc.).
