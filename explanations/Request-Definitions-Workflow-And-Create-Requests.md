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
| `CurrentStepOrder` | int | 1-based current step; 0 = completed (approved/rejected) |
| `PlannedStepsJson` | JSON string | Snapshot of the entire approval chain at submission |
| `Employee` | navigation | The requester |
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
A single step in an approval chain. Links an OrgNode to a position in the sequence.

| Field | Type | Description |
|-------|------|-------------|
| `OrgNodeId` | Guid | Which OrgNode's managers must approve at this step |
| `SortOrder` | int | Position in the chain (1 = first approver) |

#### Supporting Hierarchy Entities

Each `Employee` belongs to an OrgNode hierarchy via `OrgNodeAssignment`:
```
Company
  └── OrgNode (root)
        └── OrgNode (child)
              └── OrgNode (leaf - where employees are assigned)
```

Each `OrgNodeAssignment` links an employee to an OrgNode and records their `OrgRole` (Manager or Member) at that node.

`OrgRole` enum:
| Value | Description |
|-------|-------------|
| `Member` | Regular employee |
| `Manager` | Manager at their assigned node |

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

3. Validate steps have unique sort orders

4. Validate each OrgNode exists

5. Persist
   ├── Create RequestDefinition
   └── Create RequestWorkflowStep records for each step (OrgNodeId + SortOrder)
```

### Example

Creating a Leave workflow for a company:

```json
{
  "companyId": "...",
  "requestType": "Leave",
  "isActive": true,
  "steps": [
    { "orgNodeId": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "sortOrder": 1 },
    { "orgNodeId": "7fa85f64-5717-4562-b3fc-2c963f66afa7", "sortOrder": 2 },
    { "orgNodeId": "8fa85f64-5717-4562-b3fc-2c963f66afa8", "sortOrder": 3 }
  ]
}
```

**Note:** Step validation (ensuring each OrgNode has at least one manager) happens at **request creation time**, not at definition creation time.

---

## How the Workflow Engine Resolves Approvers

**Class:** `WorkflowResolutionService` (`HrSystemApp.Infrastructure/Services/WorkflowResolutionService.cs`)
**Method:** `BuildApprovalChainAsync`

This is the core engine that translates a workflow template into a **concrete list of approver IDs** at the moment a request is submitted.

### Resolution Logic

For each `RequestWorkflowStep` in `SortOrder`:

1. **Get requester's node** from `OrgNodeAssignment`
2. **Get all ancestor nodes** of the requester's node (ordered from immediate parent to root)
3. **For each step:**
   - If the step references the requester's **own node** and the requester is a **manager** at that node → **skip** (self-approval prevention)
   - If the step references the requester's **own node** and the requester is **not** a manager → get managers at the requester's node
   - If the step references an **ancestor node** → get managers at that ancestor node via `OrgNodeAssignments.GetManagersByNodeAsync`
   - **Exclude the requester** from the approver list (self-approval prevention)
   - If all managers were excluded (requester was the only manager) → keep them anyway

### Validation Rules

- Each step must reference the requester's **own node** or one of its **ancestors**
- Each step must have **at least one active manager** at the referenced node
- If validation fails, request creation fails with `NoActiveManagersAtStep` or `InvalidWorkflowChain`

### Self-Approval Prevention

- If the employee is a manager at their own node, steps targeting that node are **skipped**
- The requester is excluded from any approver list

### Fallback

If **all steps are skipped** (e.g., employee is manager at all definition nodes) → **auto-approve** the request immediately.

### Chain Snapshotted at Submission

The entire approval chain is serialized to `PlannedStepsJson` at submission time. Future changes to:
- The `RequestDefinition` workflow steps
- The `OrgNode` hierarchy
- Manager assignments via `OrgNodeAssignment`

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

5. Resolve Workflow (WorkflowResolutionService.BuildApprovalChainAsync)
   └── Get employee's OrgNode assignment
   └── Build approval chain based on definition steps and OrgNode hierarchy
   └── Fail if step references a node without managers

6. Persist
   ├── Create Request with Status = Submitted
   ├── CurrentStepOrder = 1
   ├── PlannedStepsJson = JSON snapshot of full chain with approvers
   └── Return the new RequestId

7. Auto-approve (if chain is empty)
   └── All steps were skipped due to self-approval prevention
   └── Status = Approved, comment = "Auto accepted by system"
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

**Endpoint:** `POST /api/requests/approvals/{id}/approve`

### Flow

```
1. Security
   └── Resolve approver from JWT user
   └── Approver must be in current step's approver list

2. Validate
   └── Status must be Submitted or InProgress
   └── CurrentStepOrder must be within bounds

3. Record in ApprovalHistory
   └── RequestId, ApproverId, Status = Approved, Comment, Timestamp

4. Advance step
   └── CurrentStepOrder++

5. Determine next step
   └── From PlannedStepsJson snapshot:
       ├── If CurrentStepOrder > steps.Count:
       │     Status = Approved
       │     CurrentStepOrder = 0
       │     Call OnFinalApprovalAsync (e.g., deduct leave balance)
       └── Else:
             Status = InProgress

6. Notify requester (only on final approval)
```

---

## Rejecting a Request

**Endpoint:** `POST /api/requests/approvals/{id}/reject`

### Flow

```
1. Security
   └── Resolve approver from JWT user
   └── Approver must be in current step's approver list

2. Update Request
   └── Status = Rejected
   └── CurrentStepOrder = 0

3. Record in ApprovalHistory
   └── Status = Rejected
   └── Comment = rejection reason

4. Notify requester with rejection reason
```

---

## Key Files Reference

| Area | File |
|------|------|
| Domain Models | `Domain/Models/Request.cs` (Request, RequestApprovalHistory), `Domain/Models/RequestWorkflow.cs` (RequestDefinition, RequestWorkflowStep) |
| Domain Enums | `Domain/Enums/RequestType.cs`, `RequestStatus.cs`, `OrgRole.cs` |
| Schema Definition | `Application/Common/RequestSchemas.json` |
| Schema Validator | `Infrastructure/Services/RequestSchemaValidator.cs` |
| Workflow Resolution | `Infrastructure/Services/WorkflowResolutionService.cs` |
| Create Request | `Application/Features/Requests/Commands/CreateRequest/CreateRequestCommand.cs` |
| Create Definition | `Application/Features/Requests/Commands/Admin/CreateRequestDefinitionCommand.cs` |
| Update Definition | `Application/Features/Requests/Commands/Admin/UpdateRequestDefinitionCommand.cs` |
| Delete Definition | `Application/Features/Requests/Commands/Admin/DeleteRequestDefinitionCommand.cs` |
| Approve Request | `Application/Features/Requests/Commands/ApproveRequest/ApproveRequestCommand.cs` |
| Reject Request | `Application/Features/Requests/Commands/RejectRequest/RejectRequestCommand.cs` |
| Leave Strategy | `Application/Features/Requests/Strategies/LeaveRequestStrategy.cs` |
| Strategy Factory | `Application/Features/Requests/Strategies/RequestStrategyFactory.cs` |
| DTOs | `Application/DTOs/Requests/PlannedStepDto.cs`, `WorkflowStepDto.cs` |
| API Controllers | `Api/Controllers/RequestsController.cs`, `Api/Controllers/RequestDefinitionsController.cs` | |

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
│  Workflow Resolution     │  ← WorkflowResolutionService.BuildApprovalChainAsync
│  (OrgNode → Managers)    │    Walks up OrgNode hierarchy from employee's node
│                           │    Gets managers via OrgNodeAssignment
│                           │    Self-approval: skips if employee is manager at step's node
└─────────────────────────┘
         │
         ▼
┌─────────────────────────┐
│  Persist Request         │  ← Status = Submitted
│  Snapshot Chain           │    PlannedStepsJson = [{NodeId, NodeName, Approvers}]
│  Set CurrentStepOrder    │    CurrentStepOrder = 1
└─────────────────────────┘
         │
         ▼ (each approval)
┌─────────────────────────┐
│  Approval Flow           │  ← CurrentStepOrder++
│  or Finalize             │    On final: call OnFinalApprovalAsync
│                           │    Reject: sets CurrentStepOrder = 0
└─────────────────────────┘
```
