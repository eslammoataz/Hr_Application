# HRMS Master Specification (Constitution-Aligned)

## 1. System Overview & Architectural Baseline
A production-grade, multi-tenant HR Management System (HRMS).
This system strictly adheres to the **HRMS Constitution (v1.1.0)**. 
- **Architecture**: Domain <- Application <- Infrastructure <- API. 
- **CQRS**: Handled via `MediatR` with lean handlers.
- **Client**: Flutter (MVVM + Bloc). The API must return lightweight, flat DTOs wrapped in a standardized `Result<T>` via Mapster projections. No stack traces or internal details leaked.

## 2. Multi-Tenancy & Security Rules (Constitution Pillar V)
- **Tenancy**: Each employee belongs to a company. Company data MUST be absolutely isolated.
- **Enforcement**: MUST use EF Core Global Query Filters or Application-layer Data Scopes resolved via MediatR Pipeline Behaviors. Handlers should assume inherently scoped data.
- **Super Admin**: Evaluated dynamically to bypass tenant isolation boundaries (via specific token rules).
- **Privacy**: No logging of PII, salary metrics, or sensitive medical employee data. Correlate logs via structured request IDs.

## 3. Roles & Hierarchy
* Super Admin (global access)
* Standard Ecosystem: CEO -> Vice President (optional) -> Department Manager -> Unit Leader -> Team Leader -> HR -> Employee -> Optional: IT / Asset Admin.

## 4. Core Modules & Performance Mandates (Constitution Pillar IV)

*General Mandates for all Modules:*
- **Zero N+1 Tolerance**: Avoid loading large object graphs.
- **Projections**: Always use `.ProjectToType<T>()` or `.Select()` instead of `.Include()`.
- **Latency**: Minimize DB round trips (e.g., using `Task.WhenAll` for aggregates).
- **Read-Only**: All lookup queries MUST utilize `.AsNoTracking()`.

### 4.1. Employee Management
* **Scope**: Full employee profile (personal + organizational data), hierarchy assignment, manager mapping.
* **Alignment**: Profile fetch queries must flattened via Mapster into `EmployeeProfileDto`.

### 4.2. Attendance
* **Scope**: Clock in/out, tracking (present/late/absent), geolocation.
* **Alignment**: Requires asynchronous I/O and transaction safety for clock boundaries. 

### 4.3. Requests System (CRITICAL MODULE)
* **Scope**: Leave/Permission, Salary Slip, HR Letter, Resignation, POs, Assets, Loans, Assignment.
* **Concept**: Each request has `approval_chain[]`, `status`, `attachments`, `history`.
* **Alignment**: History and attachments should be lazy-loaded or fetched via optimized parallel queries, avoiding heavy graph loading in lists.

### 4.4. Dynamic Approval Workflow Engine (IMPORTANT)
* **Scope**: Dynamically routes requests upward based on hierarchy.
* **Rules**: System routes automatically. Each step provides approve/reject/escalate features, triggering notifications.
* **Alignment**: State transitions (Approval -> Validation -> Deduction) MUST execute under a single `UnitOfWork` (cross-cutting transaction pipeline) to ensure database consistency.

### 4.5. Surveys & Complaints System
* **Scope**: Hierarchical feedback and HR-assigned grievance tracking. 

### 4.6. Salary & Payroll
* **Alignment**: Logic for allowances and deductions MUST reside in the Domain or Application layer and be 100% unit-testable without DB context. (Constitution Pillar III).

### 4.7. Notifications & Company Management
* **Scope**: Push infrastructure and Super Admin company provisioning. 
* **Alignment**: Offload broadcast operations to background tasks to keep API endpoints fully non-blocking (Constitution Pillar V).

## 5. Critical Flow Implementations

### Flow A: Leave Request
- Employee submits -> Validated via `FluentValidation` Pipeline -> Handled via MediatR -> Routed through chain. Final approval updates balance in the SAME transaction scope as the status update.

## 6. Testing Mandates (Constitution Pillar II & III)
- **Unit Tests**: Full coverage for Handlers, Payroll calculation rules, and Domain behavior. Mock external boundaries only.
- **Integration Tests**: Leave Request flows, Approval Engine transitions, and Multi-Tenancy segregation.
- **Architecture Tests**: Automated safeguards ensuring cross-cutting concerns (Transactions, Validation) remain pipelines, not hardcoded logic.
