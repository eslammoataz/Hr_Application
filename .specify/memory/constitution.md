<!--
Sync Impact Report
- Version: 1.0.0 (Initial Creation)
- Added Sections: I. Code Quality, II. Testing Standards, III. Database & Performance, IV. API & User Experience Consistency, V. Scalability & Reliability, VI. Maintainability
- Templates requiring updates: ✅ updated (plan-template.md defers dynamically to the constitution)
- Follow-up TODOs: None
-->

# HR Management System Constitution

## Core Principles

### I. Code Quality
* Enforce Clean Architecture boundaries (Domain, Application, Infrastructure).
* Controllers and EF Entities MUST NOT contain any business logic.
* Use meaningful naming and small, focused classes.
* Avoid unnecessary abstractions and over-engineering.
* Adhere strictly to SOLID principles.

### II. Testing Standards
* All business logic MUST be unit testable.
* MediatR handlers MUST have comprehensive unit tests.
* Critical flows MUST have integration tests.
* Avoid tightly coupled code that is hard to test.
* Mock ONLY external dependencies.

### III. Database & Performance
* Prevent N+1 queries by default.
* Prefer projection (`Select`) over `Include` when possible.
* Avoid loading large object graphs unnecessarily.
* Use pagination for ALL list endpoints.
* Minimize database round trips.
* Use `async` operations for all I/O calls.

### IV. API & User Experience Consistency
* Standardized API response structure (`Result` wrapper, error handling).
* Consistent pagination format across endpoints.
* Clear and meaningful error messages.
* NO leaking internal implementation details in responses.

### V. Scalability & Reliability
* All operations MUST be async and non-blocking.
* Avoid shared mutable state.
* Design for multi-tenant support (HRMS companies).
* Ensure proper transaction handling.

### VI. Maintainability
* Code MUST be easy to read and modify.
* Avoid duplicate logic (DRY principle).
* Centralize common patterns (validation, mapping, error handling).

## Governance

* This Constitution supersedes all other engineering guidelines and implicit practices.
* Amendments to this document require review and approval by architectural stakeholders.
* All Pull Requests MUST be evaluated against these principles.

**Version**: 1.0.0 | **Ratified**: 2026-04-11 | **Last Amended**: 2026-04-11
