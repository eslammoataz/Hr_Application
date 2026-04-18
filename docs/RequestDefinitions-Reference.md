# Request Definitions API Reference

## Overview

**Endpoint:** `GET /api/requestdefinitions?companyId={companyId}`

Request Definitions define the **approval workflow** for each type of request in your company. They specify **who approves** each request type (Leave, Permission, PurchaseOrder, etc.) and are the blueprint used when an employee submits a request.

---

## The JSON Response

```json
{
  "isSuccess": true,
  "data": [
    {
      "id": "...",
      "companyId": "...",
      "requestType": 0,
      "requestName": "Leave",
      "isActive": true,
      "steps": [ ... ],
      "schema": [ ... ]
    }
  ],
  "error": null
}
```

### Top-Level Fields

| Field | Type | Description |
|-------|------|-------------|
| `id` | GUID | Unique identifier for this definition |
| `companyId` | GUID | Which company this belongs to |
| `requestType` | int | Enum value for the type of request (see below) |
| `requestName` | string | Human-readable name (e.g. "Leave", "Permission") |
| `isActive` | bool | Whether this definition is currently in use |
| `steps` | array | **The approval chain** — who approves, in what order |
| `schema` | array | The form fields the employee must fill when submitting |

---

## Request Types (Enum)

| Value | Name | Description |
|-------|------|-------------|
| `0` | **Leave** | Vacation / annual leave |
| `1` | **Permission** | Short time off / WFH |
| `2` | **SalarySlip** | Pay slip request |
| `3` | **HRLetter** | HR-issued letter |
| `4` | **Resignation** | Resignation submission |
| `5` | **EndOfService** | EOS / clearance request |
| `6` | **PurchaseOrder** | Budget / PO approval |
| `7` | **Asset** | Asset request |
| `8` | **Loan** | Loan request |
| `9` | **Assignment** | Work assignment |
| `10` | **Other** | Miscellaneous |

---

## Steps Array — The Approval Chain

This is the core of the definition. Each step represents **one approval stage**.

### Step Object

```json
{
  "stepType": 0,
  "orgNodeId": "guid-or-null",
  "bypassHierarchyCheck": false,
  "directEmployeeId": "guid-or-null",
  "startFromLevel": null,
  "levelsUp": null,
  "sortOrder": 1
}
```

### `stepType` — The Three Approval Types

| Value | Name | How It Works |
|-------|------|-------------|
| `0` | **OrgNode** | The managers of a specific org node approve |
| `1` | **DirectEmployee** | A specific named employee approves |
| `2` | **HierarchyLevel** | Dynamic — resolves approvers based on the requester's position in the org tree |

### Which Fields Are Used Per Type

| Field | OrgNode (`0`) | DirectEmployee (`1`) | HierarchyLevel (`2`) |
|-------|:---:|:---:|:---:|
| `orgNodeId` | ✅ | ❌ | ❌ |
| `directEmployeeId` | ❌ | ✅ | ❌ |
| `startFromLevel` | ❌ | ❌ | ✅ |
| `levelsUp` | ❌ | ❌ | ✅ |
| `bypassHierarchyCheck` | ✅ | ❌ | ❌ |

---

## Step Type Examples

### OrgNode (`stepType: 0`)

```json
{
  "stepType": 0,
  "orgNodeId": "d01d91fb-7da5-44e6-8c71-4a1af5c2dff3",
  "bypassHierarchyCheck": false,
  "directEmployeeId": null,
  "startFromLevel": null,
  "levelsUp": null,
  "sortOrder": 1
}
```

> **Meaning:** The managers of the Finance Division (`d01d91fb...`) approve this step.
>
> **bypassHierarchyCheck:** When `false`, the approver must be in the requester's upward org chain (they must be an ancestor of the requester's node). When `true`, any manager of that node can approve regardless of hierarchy position.

---

### DirectEmployee (`stepType: 1`)

```json
{
  "stepType": 1,
  "orgNodeId": null,
  "bypassHierarchyCheck": false,
  "directEmployeeId": "7f40db62-858c-4280-b65c-3996e6aa353a",
  "startFromLevel": null,
  "levelsUp": null,
  "sortOrder": 1
}
```

> **Meaning:** The specific employee `7f40db62...` (e.g. HR Director Sarah) approves this step, regardless of org position.

---

### HierarchyLevel (`stepType: 2`)

```json
{
  "stepType": 2,
  "orgNodeId": null,
  "bypassHierarchyCheck": false,
  "directEmployeeId": null,
  "startFromLevel": 1,
  "levelsUp": 3,
  "sortOrder": 1
}
```

> **Meaning:** The requester's own approval chain is dynamically built from their position in the org tree.

### How `HierarchyLevel` Works

- **Level 1** = The requester's **own node** (e.g. API Team)
- **Level 2** = The requester's **immediate parent node** (e.g. Backend Department)
- **Level 3** = The requester's **grandparent node** (e.g. Engineering Division)
- And so on...

`startFromLevel` = which level to **start** from (defaults to 1 if omitted)
`levelsUp` = how many **consecutive levels** to include

**Example:** `startFromLevel: 1, levelsUp: 3` means **levels 1, 2, and 3**.

For **Charlie (API Team member)**:
```
Level 1 → API Team         → Tom (Manager)
Level 2 → Backend Dept    → Mike CEO (Manager)
Level 3 → Engineering Div → Mike (already in chain, deduped → skipped)
```

Result: **2 effective steps** (Tom → Mike CEO)

---

## Mixed Chains

A definition can mix all three step types in one chain. For example:

```json
"steps": [
  {
    "stepType": 2,
    "startFromLevel": 1,
    "levelsUp": 2,
    "sortOrder": 1
  },
  {
    "stepType": 1,
    "directEmployeeId": "sarah-hr-guid",
    "sortOrder": 2
  },
  {
    "stepType": 2,
    "startFromLevel": 3,
    "levelsUp": 1,
    "sortOrder": 3
  }
]
```

This means:
1. **Levels 1-2** from requester's hierarchy (own node + parent)
2. **Fixed employee** Sarah HR (special approver in the middle)
3. **Level 3** from requester's hierarchy (grandparent)

---

## Validation Rules for Steps

### HierarchyLevel Steps
- `levelsUp` must be **>= 1**
- `startFromLevel` must be **>= 1** (or omitted, defaults to 1)
- `orgNodeId`, `directEmployeeId`, and `bypassHierarchyCheck` must **NOT** be set

### OrgNode Steps
- `orgNodeId` must be set
- `startFromLevel` and `levelsUp` must **NOT** be set

### DirectEmployee Steps
- `directEmployeeId` must be set
- `startFromLevel`, `levelsUp`, `orgNodeId`, and `bypassHierarchyCheck` must **NOT** be set

### Range Overlap
- Multiple `HierarchyLevel` steps in the same definition **must not have overlapping level ranges**

---

## Schema Array — The Form

The `schema` defines what fields an employee must fill when submitting a request of this type.

```json
"schema": [
  {
    "name": "startDate",
    "type": "date",
    "label": "Start Date",
    "isRequired": true
  },
  {
    "name": "duration",
    "type": "number",
    "label": "Duration (days)",
    "isRequired": true
  }
]
```

### Field Types

| Type | Description |
|------|-------------|
| `string` | Text input |
| `number` | Numeric input |
| `date` | Date picker |
| `boolean` | Yes/No toggle |
| `list` | Dynamic list/array of items |

---

## How Definitions Are Used

When an employee submits a request:

1. The system looks up the **active definition** for that `requestType` in their company
2. The `BuildApprovalChainAsync` service **resolves** the steps into actual approvers:
   - `OrgNode` → looks up managers at that specific node
   - `DirectEmployee` → uses the fixed employee
   - `HierarchyLevel` → walks up the requester's org tree from Level 1 upward
3. The resolved chain is **frozen** as JSON in `Request.PlannedStepsJson`
4. The request enters workflow at **Step 1**

---

## Preview Endpoint

**Endpoint:** `POST /api/requestdefinitions/preview`

Preview what the approval chain will look like for a specific employee **without creating a request**.

```json
{
  "requesterEmployeeId": "charlie-guid",
  "steps": [
    { "stepType": 2, "startFromLevel": 1, "levelsUp": 3, "sortOrder": 1 }
  ]
}
```

Response:
```json
{
  "isSuccess": true,
  "data": [
    {
      "stepType": 2,
      "nodeId": "api-team-guid",
      "nodeName": "API Team",
      "sortOrder": 1,
      "approvers": [
        { "employeeId": "tom-guid", "employeeName": "Tom Backend" }
      ],
      "resolvedFromLevel": 1
    },
    {
      "stepType": 2,
      "nodeId": "backend-guid",
      "nodeName": "Backend",
      "sortOrder": 2,
      "approvers": [
        { "employeeId": "mike-guid", "employeeName": "Mike CEO" }
      ],
      "resolvedFromLevel": 2
    }
  ]
}
```

Key field: **`resolvedFromLevel`** tells you which level (1-based) each step was resolved from.
