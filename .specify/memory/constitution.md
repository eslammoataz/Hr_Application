<!--
Sync Impact Report
- Version: 1.1.0
- Added Sections: Improved MediatR and Tooling guidance, Added Security & Observability constraints
- Templates requiring updates: ✅ updated 
- Follow-up TODOs: None
-->

# HR Management System Constitution

## Core Principles

### I. Code Quality & Clean Architecture
* Enforce Clean Architecture boundaries: Domain <- Application <- Infrastructure <- API. 
* Use automated architecture tests (e.g., NetArchTest) to prevent boundary violations.
* Controllers and EF Entities MUST NOT contain any business logic or I/O calls.
* Abide strictly by SOLID principles. Prefer composition over inheritance.

### II. MediatR & Cross-Cutting Concerns
* Keep Handlers lean. 
* Offload cross-cutting concerns (Logging, Validation, Caching, DB Transactions) exclusively to MediatR Pipeline Behaviors.
* Use `FluentValidation` for all input validation; avoid Data Annotations on DTOs.
* Centralize object mapping using Mapster profiles.

### III. Testing Standards
* All core business logic MUST be unit testable and isolated from I/O.
* MediatR handlers MUST have comprehensive unit tests.
* Critical flows (e.g., Employee Onboarding, Hierarchy updates) MUST have integration tests.
* Mock ONLY external boundaries (Databases, Third-party APIs, Clock services).

### IV. Database & Performance
* Prevent N+1 queries by default.
* Use Mapster's `ProjectToType()` or LINQ `.Select()` over `.Include()` to prevent over-fetching.
* Always append `.AsNoTracking()` to read-only queries.
* Avoid loading large object graphs unnecessarily.
* Coordinate writes via a centralized `UnitOfWork`. Do not call `SaveChanges` inside repositories.
* Use `async/await` operations for all I/O calls.

### V. Security, Privacy & Observability
* Design explicitly for multi-tenant data isolation (HRMS companies). Enforce Data Scopes at the Application layer.
* Never log PII, salary metrics, or sensitive medical employee data.
* Use structured logging with contextual correlation IDs for all API requests and MediatR commands.

### VI. API & User Experience Consistency
* Wrap all endpoint responses in a standardized `Result<T>` structure.
* Consistent pagination format across all endpoints.
* Return clear error codes. NEVER leak internal stack traces or database exceptions to the client.

## Governance
* This Constitution supersedes all other engineering guidelines and implicit practices.
* Amendments to this document require review and approval by architectural stakeholders.
* Compliance is mandatory and must be verified via Pull Request checklists before merging.

**Version**: 1.1.0 | **Ratified**: 2026-04-11 | **Last Amended**: 2026-04-11
