# Request Definitions, Workflow & Create Requests

## Overview

The HRMS has a configurable approval workflow system. Employees submit requests (leave, permission, HR letters, etc.) which flow through a company-specific approval chain before being approved or rejected. The workflow is **role-based** (not person-based), **snapshotted at submission**, and supports **type-specific business logic**.

---

## Data Model

### Core Entities

#### `Request`
The base entity for all requests. Stores type-specific data as JSON.

| Field | Type | Description |
|-------|------|-------------|
| `EmployeeId` | Guid | The requester |
| `RequestType` | `RequestType` enum | Determines which workflow applies |
| `Data` | JSON string | Type-specific payload (e.g., leave dates, duration) |
| `Status` | `RequestStatus` enum | `Draft`, `Submitted`, `InProgress`, `Approved`, `Rejected`, `Escalated`, `Cancelled` |
| `CurrentApproverId` | Guid? | Who must currently act on it (null if fully approved) |
| `PlannedChainJson` | JSON string | Snapshot of the entire approval chain at submission |
| `Employee` | navigation | The requester |
| `CurrentApprover` | navigation | The current approver |
| `ApprovalHistory` | collection | Immutable audit trail |

#### `RequestDefinition`
A workflow **template** per company per request type. One definition, many requests.

| Field | Type | Description |
|-------|------|-------------|
| `CompanyId` | Guid | Owning company |
| `RequestType` | `RequestType` enum | Which request type this handles |
| `IsActive` | bool | Enables/disables this request type for the company |
| `FormSchemaJson` | JSON string? | Optional override of the default validation schema |
| `WorkflowSteps` | collection | Ordered `RequestWorkflowStep` records |

**Unique constraint:** `(CompanyId, RequestType)` — one workflow definition per type per company.

#### `RequestWorkflowStep`
A single step in an approval chain. Links a role to a position in the sequence.

| Field | Type | Description |
|-------|------|-------------|
| `RequiredRole` | `UserRole` enum | Which role must approve at this step |
| `SortOrder` | int | Position in the chain (1 = first approver) |

#### Supporting Hierarchy Entities

Each `Employee` belongs to a hierarchy:
```
Company
  └── Department (has ManagerId, VicePresidentId)
        └── Unit (has UnitLeaderId)
              └── Team (has TeamLeaderId)
```

`CompanyHierarchyPosition` defines the authority ladder per company:
| Field | Description |
|-------|-------------|
| `Role` | e.g., `CEO`, `VicePresident`, `TeamLeader` |
| `PositionTitle` | Human-readable title |
| `SortOrder` | 1 = highest authority, higher numbers = lower authority |

---

## RequestType Enum

```
0  Leave
1  Permission
2  SalarySlip
3  HRLetter
4  Resignation
5  EndOfService
6  PurchaseOrder
7  Asset
8  Loan
9  Assignment
10 Other
```

## RequestStatus Enum

```
Draft        — created but not submitted
Submitted    — submitted, awaiting first approver
InProgress   — partially approved (some approvers have acted)
Approved     — fully approved
Rejected     — rejected by an approver
Escalated   — escalated (not currently used in code)
Cancelled   — cancelled by the requester
```

---

## RequestSchema JSON

Each request type has a schema defined in `HrSystemApp.Application/Common/RequestSchemas.json`. This defines the fields required in the `Data` JSON.

Example — `Leave` request:
```json
[
  { "name": "leaveSubType",  "type": "number",  "isRequired": true,  "enum": [0,1,2,3,4,5,6,7] },
  { "name": "startDateTime", "type": "date",    "isRequired": true },
  { "name": "duration",      "type": "number",  "isRequired": true },
  { "name": "isHourly",      "type": "boolean", "isRequired": false },
  { "name": "reason",        "type": "string",  "isRequired": false }
]
```

Field types supported: `string`, `number`, `boolean`, `date`, `datetime`.

---

## How a Request Definition is Created

**Endpoint:** `POST /api/requestdefinitions`

### Flow

```
1. Security check
   └── SuperAdmin can create for any company
   └── Other roles use their own Employee.CompanyId

2. Duplicate check
   └── Fail if (CompanyId, RequestType) already exists

3. Workflow validation (WorkflowValidationHelper.ValidateWorkflowSteps)
   ├── Every role in the steps must exist in CompanyHierarchyPosition
   └── Steps must ESCALATE in authority — each step's SortOrder must be
       LOWER (higher rank) than the previous step
       Example valid: TeamLeader(5) → DepartmentManager(3) → HR(6)

4. Persist
   ├── Create RequestDefinition
   └── Create RequestWorkflowStep records for each step
```

### Example

Creating a Leave workflow for a company:

```json
{
  "companyId": "...",
  "requestType": "Leave",
  "isActive": true,
  "steps": [
    { "role": "TeamLeader",       "sortOrder": 5 },
    { "role": "DepartmentManager", "sortOrder": 3 },
    { "role": "HR",               "sortOrder": 6 }
  ]
}
```

---

## How the Workflow Engine Resolves Approvers

**Class:** `WorkflowService` (`HrSystemApp.Infrastructure/Services/WorkflowService.cs`)
**Method:** `GetApprovalPathAsync`

This is the core engine that translates a workflow template into a **concrete list of approver IDs** at the moment a request is submitted.

### Resolution Logic

For each `RequestWorkflowStep` in `SortOrder`:

| Role | Resolved By |
|------|-------------|
| `TeamLeader` | `Employee.Team.TeamLeaderId` |
| `UnitLeader` | `Employee.Unit.UnitLeaderId` |
| `DepartmentManager` | `Employee.Department.ManagerId` |
| `VicePresident` | `Employee.Department.VicePresidentId` |
| `CEO` / `HR` / `AssetAdmin` / `CompanyAdmin` | First employee in the company with that role (via `UserManager`) |
| `SuperAdmin` | First SuperAdmin in the system |

### Self-Approval Prevention

If the resolved approver is the **same person as the requester**, that step is **skipped**. The chain continues to the next step.

### Deduplication

If two steps resolve to the **same person**, they appear only once in the final chain.

### Fallback

If **no approvers** are resolved at all → fallback to `CompanyAdmin`, then `HR`.

### Chain Snapshotted at Submission

The entire approval chain is serialized to `PlannedChainJson` at submission time. Future changes to:
- The `RequestDefinition` workflow steps
- The `CompanyHierarchyPosition` table
- Who holds a leadership role

**Do not affect in-flight requests.** The chain is frozen.

---

## How a Request is Created and Routed

**Endpoint:** `POST /api/requests`

### Flow

```
1. Auth
   └── Resolve Employee from the current JWT user

2. Resolve Definition
   └── Look up RequestDefinition by (Employee.CompanyId, RequestType)
   └── Fail if not found or IsActive == false

3. Schema Validation (IRequestSchemaValidator)
   └── Validate the JSON Data against FormSchemaJson
       (or global RequestSchemas.json if no override)
   └── Check required fields, types, enum values

4. Business Logic Validation (IRequestStrategyFactory)
   └── If a strategy exists for this RequestType, run it
   └── Example: LeaveRequestStrategy checks leave balance
       RemainingDays - PendingDurations >= RequestedDuration

5. Resolve Workflow (WorkflowService.GetApprovalPathAsync)
   └── Get the approval chain for this employee + request type
   └── Fail if no approvers found

6. Persist
   ├── Create Request with Status = Submitted
   ├── CurrentApproverId = approvalPath.First().Id (first approver)
   ├── PlannedChainJson = JSON snapshot of full chain
   └── Return the new RequestId
```

---

## Type-Specific Business Logic

### `LeaveRequestStrategy`

Runs when a leave request is **created** and when it reaches **final approval**.

#### On Request Creation:
- Duration must be > 0
- `RemainingDays - PendingDurations >= RequestedDuration`
- `PendingDurations` = sum of all other `Submitted`/`InProgress` leave requests for the same employee in the same year

#### On Final Approval:
- Deduct `duration` from `LeaveBalance.UsedDays`

---

## Approving a Request

**Endpoint:** `POST /api/requests/{id}/approve`

### Flow

```
1. Security
   └── Only CurrentApproverId can approve

2. Validate
   └── Status must be Submitted or InProgress

3. Record in ApprovalHistory
   └── RequestId, ApproverId, Status, Comment, Timestamp

4. Determine next step
   └── From PlannedChainJson snapshot:
       ├── If more approvers remain:
       │     CurrentApproverId = next approver's Id
       │     Status = InProgress
       └── If last approver:
             CurrentApproverId = null
             Status = Approved
             Call OnFinalApprovalAsync (e.g., deduct leave balance)

5. Notify requester
```

---

## Rejecting a Request

**Endpoint:** `POST /api/requests/{id}/reject`

### Flow

```
1. Security
   └── Only CurrentApproverId can reject

2. Update Request
   └── Status = Rejected
   └── CurrentApproverId = null

3. Record in ApprovalHistory

4. Notify requester with rejection reason
```

---

## Key Files Reference

| Area | File |
|------|------|
| Domain Models | `Domain/Models/Request.cs` (Request, RequestDefinition, RequestWorkflowStep) |
| Domain Enums | `Domain/Enums/RequestType.cs`, `RequestStatus.cs`, `UserRole.cs` |
| Schema Definition | `Application/Common/RequestSchemas.json` |
| Schema Validator | `Infrastructure/Services/RequestSchemaValidator.cs` |
| Workflow Engine | `Infrastructure/Services/WorkflowService.cs` |
| Workflow Validation | `Application/Features/Requests/Commands/Admin/WorkflowValidationHelper.cs` |
| Create Request | `Application/Features/Requests/Commands/CreateRequest/CreateRequestCommand.cs` |
| Create Definition | `Application/Features/Requests/Commands/Admin/CreateRequestDefinitionCommand.cs` |
| Update Definition | `Application/Features/Requests/Commands/Admin/UpdateRequestDefinitionCommand.cs` |
| Delete Definition | `Application/Features/Requests/Commands/Admin/DeleteRequestDefinitionCommand.cs` |
| Approve Request | `Application/Features/Requests/Commands/ApproveRequest/ApproveRequestCommand.cs` |
| Reject Request | `Application/Features/Requests/Commands/RejectRequest/RejectRequestCommand.cs` |
| Leave Strategy | `Application/Features/Requests/Strategies/LeaveRequestStrategy.cs` |
| Strategy Factory | `Application/Features/Requests/Strategies/RequestStrategyFactory.cs` |
| DB Config | `Infrastructure/Data/Configurations/RequestConfiguration.cs` |
| Repositories | `Infrastructure/Repositories/RequestRepository.cs`, `RequestDefinitionRepository.cs` |
| API Controller | `Api/Controllers/RequestDefinitionsController.cs` |

---

## Architecture Diagram

```
Employee submits a Request
         │
         ▼
┌─────────────────────────┐
│  Schema Validation      │  ← RequestSchemas.json / FormSchemaJson override
│  (RequestSchemaValidator)│
└─────────────────────────┘
         │
         ▼
┌─────────────────────────┐
│  Business Logic          │  ← LeaveRequestStrategy (checks leave balance)
│  (IRequestStrategyFactory)│
└─────────────────────────┘
         │
         ▼
┌─────────────────────────┐
│  Workflow Resolution     │  ← WorkflowService.GetApprovalPathAsync
│  (role → person)         │    Looks up TeamLeader/UnitLeader/etc. from
│                           │    Employee's Team/Unit/Department hierarchy
└─────────────────────────┘
         │
         ▼
┌─────────────────────────┐
│  Persist Request         │  ← Status = Submitted
│  Snapshot Chain           │    PlannedChainJson = full approval chain
│  Set First Approver      │    CurrentApproverId = first approver
└─────────────────────────┘
         │
         ▼ (each approval)
┌─────────────────────────┐
│  Approval Flow           │  ← Move to next approver in PlannedChainJson
│  or Finalize             │    On final: call OnFinalApprovalAsync
└─────────────────────────┘
```
