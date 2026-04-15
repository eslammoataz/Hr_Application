# HRMS Project Constitution
This document defines the mandatory architectural and coding standards for this project. 
All code generated or refactored by Claude must strictly adhere to these rules.

## Core Principles
### I. Code Quality & Architecture
Follow Clean Architecture boundaries: Domain ← Application ← Infrastructure ← API... [Rest of your text]

## Tech Stack Reference
- **Framework:** .NET 8+
- **Database:** PostgreSQL / EF Core
- **Patterns:** MediatR, CQRS, Clean Architecture
- **Library Preferences:** FluentValidation, Mapster

## Core Principles

### I. Code Quality & Architecture

Follow Clean Architecture boundaries: Domain ← Application ← Infrastructure ← API. Controllers and EF Entities MUST NOT contain business logic. Prefer simplicity over abstraction. Avoid unnecessary layers. Use dependency injection for all services.

### II. MediatR Usage

Use MediatR for commands and queries. Handlers act as orchestration layers only. Use Pipeline Behaviors for Validation (required) and Logging (optional). Avoid overusing behaviors — keep the pipeline simple.

### III. Validation & Mapping

Use FluentValidation for all input validation. Avoid Data Annotations. Use Mapster for complex mappings. Simple mappings can be done inline when clear and readable.

### IV. Database & Performance

Prevent N+1 queries using projection (`Select`) or Mapster. Use `.AsNoTracking()` for read-only queries. Avoid unnecessary `.Include()` calls. All database calls must be async. **Soft delete is mandatory for all entities** using an `IsDeleted` flag.

### V. Security & Multi-Tenancy

Use JWT authentication. Enforce tenant isolation in all queries. Never expose or log sensitive data (PII, salaries, medical info). Keep authorization simple — expand only when needed.

### VI. API Design

Use a consistent `Result<T>` response structure. Implement API versioning via URL (`/api/v1/`). Return clear, safe error messages. Do not expose internal exceptions.

### VII. Error Handling

Use `Result<T>` for expected business errors. Use exceptions only for unexpected failures. Implement global exception handling middleware. Log errors without exposing sensitive data.

### VIII. Testing

Write unit tests for core business logic and handlers. Follow Arrange / Act / Assert pattern. Focus on critical flows first. Avoid over-investing in complex testing strategies early.

## Pull Request Checklist

* [ ] No business logic in controllers
* [ ] Validation implemented using FluentValidation
* [ ] No sensitive data logged
* [ ] Queries avoid N+1 problems
* [ ] Soft delete implemented where applicable
* [ ] API responses follow `Result<T>` pattern
* [ ] Basic unit tests added for new logic

## Guidelines

* Prefer clarity over cleverness
* Avoid premature optimization
* Build only what is needed now
* Refactor when complexity actually appears

## Governance

This Constitution supersedes all other practices. Amendments require documented rationale, approval, and migration plan. All PRs and reviews must verify compliance with this Constitution. Complexity must be justified. Use runtime guidance docs for development reference.

**Version**: 1.0.0 | **Ratified**: 2026-04-13 | **Last Amended**: 2026-04-13
