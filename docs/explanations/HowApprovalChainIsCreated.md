# How the Approval Chain Is Created

## Overview

When an employee submits a request, the system resolves the approval chain by:

1. Looking up the **RequestDefinition** for that request type
2. Walking up the **employee's org tree** to build the ancestor list
3. For each step in the definition, **resolving approvers** based on step type
4. Applying **skip rules** (self-approval, empty levels, duplicate approvers)
5. **Freezing** the resolved chain in `Request.PlannedStepsJson`

---

## Step-by-Step Flow

### Step 1: Employee Submits Request

```
POST /api/employees/requests/me
{
  "requestType": 0,    ← Leave
  "data": { ... }
}
```

The system:
- Finds the active **RequestDefinition** for `requestType: 0` in the employee's company
- Gets the employee's **OrgNode assignment** (which node they sit under)
- Passes the definition's steps + employee info to `BuildApprovalChainAsync`

---

### Step 2: Load Employee's Org Tree

The system loads the employee's position:

```
Engineering Division (root ancestor)
    └── Backend Department (parent)
        └── API Team (employee's node)
            └── Charlie Dev (employee)
```

Then fetches ancestors ordered **parent-first**:

```
ancestors = [Backend Department, Engineering Division]
```

---

### Step 3: Build the Level Map

The system creates a **level index** (1-based):

```
levelNodes[0] = API Team              ← Level 1 (employee's own node)
levelNodes[1] = Backend Department   ← Level 2
levelNodes[2] = Engineering Division  ← Level 3
levelNodes[3] = null / root          ← Level 4 (if exists)
```

**Level 1 always = the employee's own node.**

---

### Step 4: Resolve Each Definition Step

For each step in the definition (in `SortOrder`):

#### Type: OrgNode (stepType: 0)

```json
{ "stepType": 0, "orgNodeId": "backend-dept-id", "bypassHierarchyCheck": false }
```

1. Look up managers at that specific node
2. If `bypassHierarchyCheck: false` — verify the node is in the employee's ancestor chain
3. If not in chain → **request rejected**
4. If `bypassHierarchyCheck: true` → skip chain validation

#### Type: DirectEmployee (stepType: 1)

```json
{ "stepType": 1, "directEmployeeId": "sarah-hr-id" }
```

1. Load that specific employee by ID
2. Add them directly to the chain

#### Type: HierarchyLevel (stepType: 2)

```json
{ "stepType": 2, "startFromLevel": 1, "levelsUp": 3 }
```

1. Calculate range: `startLevel` to `startLevel + levelsUp - 1`
2. For each level in range:
   - Look up managers at that level's node
   - If no managers → **skip this level**
   - If requester is a manager → **remove requester from approvers**
   - If all managers were already in chain → **skip this level (dedup)**

---

### Step 5: Apply Skip Rules

Three rules applied at every level:

#### Rule 1: Self-Approval Skip
If the requester is a manager at a level, they are **removed** from that level's approvers.

**Why:** A person should not approve their own request.

#### Rule 2: Empty Level Skip
If a level has **no managers assigned**, that level is **skipped entirely**.

**Why:** Don't fail a request just because one ancestor node has no manager set.

#### Rule 3: Duplicate Approver Dedup
If the same person would approve at multiple levels, they appear **only at the earliest level**.

**Why:** The same person cannot approve the same request twice.

---

### Step 6: Freeze the Chain

The resolved chain is serialized to `Request.PlannedStepsJson`:

```json
[
  {
    "stepType": 2,
    "nodeId": "api-team-id",
    "nodeName": "API Team",
    "sortOrder": 1,
    "approvers": [
      { "employeeId": "tom-id", "employeeName": "Tom Backend" }
    ],
    "resolvedFromLevel": 1
  },
  {
    "stepType": 2,
    "nodeId": "backend-dept-id",
    "nodeName": "Backend Department",
    "sortOrder": 2,
    "approvers": [
      { "employeeId": "mike-id", "employeeName": "Mike CEO" }
    ],
    "resolvedFromLevel": 2
  }
]
```

This snapshot is **never re-resolved**. If the org tree changes later, the request keeps its original chain.

---

## Example: Full Resolution for Charlie

### Input

- **Employee:** Charlie (API Team)
- **Definition:** `HierarchyLevel { startFromLevel: 1, levelsUp: 3 }`

### Resolution Process

```
Level 1 (API Team):
  - Managers: [Tom Backend, Charlie Dev]
  - Self-approval skip: Remove Charlie
  - Remaining: [Tom Backend]
  → APPROVERS: Tom Backend

Level 2 (Backend Department):
  - Managers: [Mike CEO]
  - Mike is not in chain yet
  → APPROVERS: Mike CEO

Level 3 (Engineering Division):
  - Managers: [Mike CEO]
  - Mike is already in chain → DEDUP
  → SKIPPED
```

### Final Chain

```json
[
  { "nodeName": "API Team", "approvers": ["Tom Backend"], "sortOrder": 1 },
  { "nodeName": "Backend Department", "approvers": ["Mike CEO"], "sortOrder": 2 }
]
```

---

## Code Flow (Pseudocode)

```
Employee submits Request(requestType, data)
  → Find RequestDefinition for company
  → Get Employee.OrgNodeId
  → GetAncestors(employeeNodeId)      ← ordered parent-first
  → BuildApprovalChainAsync(employeeId, nodeId, definitionSteps)
      ├── levelNodes = [ownNode] + ancestors
      ├── seenApproverIds = {}
      │
      └── For each step in definition.Steps (sorted by sortOrder):
          │
          ├── If OrgNode:
          │   ├── If not bypassHierarchyCheck and not in chain → FAIL
          │   ├── GetManagersAtNode(orgNodeId)
          │   ├── Remove self if manager
          │   └── Add to chain (if not empty after dedup)
          │
          ├── If DirectEmployee:
          │   ├── GetEmployee(directEmployeeId)
          │   └── Add to chain
          │
          └── If HierarchyLevel:
              ├── For level = startLevel to startLevel + levelsUp - 1:
              │   ├── If level > levelNodes.Count → BREAK (truncated)
              │   ├── node = levelNodes[level - 1]
              │   ├── managers = GetManagersAtNode(node.Id)
              │   ├── If no managers → CONTINUE (skip level)
              │   ├── Remove self from managers
              │   ├── If no managers left → CONTINUE (skip level)
              │   ├── Dedup against seenApproverIds
              │   └── Add non-duplicate approvers
              └── Renumber sortOrder 1..N
```

---

## Key Files Involved

| File | Role |
|------|------|
| `CreateRequestCommandHandler` | Entry point — finds definition, builds chain |
| `WorkflowResolutionService.BuildApprovalChainAsync` | Core resolution algorithm |
| `OrgNodeRepository.GetAncestorsAsync` | Returns ancestors ordered parent-first |
| `OrgNodeAssignmentRepository.GetManagersByNodeAsync` | Returns managers at a node |
| `OrgNodeAssignmentRepository.IsManagerAtNodeAsync` | Checks if employee is manager at node |
