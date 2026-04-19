# Org Node Hierarchy Workflow Overview

## What Is the Hierarchy?

The org hierarchy is a **tree structure** of nodes representing your company's organizational structure. Each node has a **type** and optionally a **parent**.

```
DemoCo
├── Engineering Division         (Division)
│   ├── Backend Department      (Department)
│   │   └── API Team           (Team)
│   └── Frontend Department    (Department)
│       └── Web Team           (Team)
├── HR Division                 (Division)
│   └── Talent Department      (Department)
│       └── Recruitment Team   (Team)
└── Finance Division           (Division)
    └── Accounting Department (Department)
        └── Payables Team    (Team)
```

---

## Node Types

| Type | Description |
|------|-------------|
| **Division** | Top-level business unit (e.g., Engineering, HR, Finance) |
| **Department** | Sub-unit under a division (e.g., Backend, Talent) |
| **Team** | Smallest unit under a department (e.g., API Team, Web Team) |

Nodes can have a **parent** (except root nodes which have `parentId: null`). A node's managers are the employees **assigned to that node** with role `Manager`.

---

## How Employees Are Assigned

Each employee is assigned to **one node** as either:
- **Manager** (role = 0) — can approve requests at that node level
- **Member** (role = 1) — typically the requester

```
Node: Backend Department
├── Manager: Tom Backend
└── Members: (none in this example)
```

---

## Levels: What They Mean

When using **HierarchyLevel** workflow steps, levels are **1-based from the requester's own node**:

| Level | Meaning | Example for Charlie (API Team) |
|-------|---------|-------------------------------|
| Level 1 | Requester's **own node** | API Team |
| Level 2 | Requester's **parent node** | Backend Department |
| Level 3 | Requester's **grandparent node** | Engineering Division |
| Level 4 | Requester's **great-grandparent** | Company Root |
| ... | and so on | ... |

---

## The Three Step Types

### 1. OrgNode (`stepType: 0`)

**Hardcoded node** — a specific org node's managers approve.

```json
{
  "stepType": 0,
  "orgNodeId": "backend-dept-id",
  "bypassHierarchyCheck": false
}
```

Who approves: Backend Department's managers.

---

### 2. DirectEmployee (`stepType: 1`)

**Fixed person** — a specific named employee approves.

```json
{
  "stepType": 1,
  "directEmployeeId": "sarah-hr-dir-id"
}
```

Who approves: Sarah HR Director (always).

---

### 3. HierarchyLevel (`stepType: 2`)

**Dynamic** — resolves approvers based on the requester's position.

```json
{
  "stepType": 2,
  "startFromLevel": 1,
  "levelsUp": 3
}
```

Who approves: Level 1 (own node) → Level 2 (parent) → Level 3 (grandparent), skipping any levels with no managers or duplicate approvers.

---

## Example: Charlie's Approval Chain

**Charlie's position:**
```
Engineering Division
    └── Backend Department
        └── API Team ← Charlie
```

**Definition:** `HierarchyLevel { startFromLevel: 1, levelsUp: 3 }`

**Resolution:**
```
Step 1 (Level 1): API Team        → Tom Backend (Manager)
Step 2 (Level 2): Backend Dept   → Mike CEO (Manager)
Step 3 (Level 3): Engineering Div → Mike CEO (already in chain, SKIPPED)
```

**Final chain: 2 steps** — Tom → Mike CEO

---

## Example: Evan's Approval Chain

**Evan's position:**
```
HR Division
    └── Talent Department
        └── Recruitment Team ← Evan
```

**Same definition:** `HierarchyLevel { startFromLevel: 1, levelsUp: 3 }`

**Resolution:**
```
Step 1 (Level 1): Recruitment Team → Grace Talent (Manager)
Step 2 (Level 2): Talent Dept     → Grace Talent (already in chain, SKIPPED)
Step 3 (Level 3): HR Division     → Sarah HR Dir (Manager)
```

**Final chain: 2 steps** — Grace → Sarah

---

## Same Definition, Different Chains

The power of HierarchyLevel: **one definition, different approvers per person** based on where they sit.

| Employee | Position | Resolved Chain |
|----------|----------|----------------|
| Charlie (API Team) | API Team → Backend → Engineering | Tom → Mike CEO |
| Evan (Recruitment) | Recruitment → Talent → HR Div | Grace → Sarah HR |
| Diana (Web Team) | Web Team → Frontend → Engineering | Alice → Mike CEO |

All three used the **same definition** but got **different approval chains**.

---

## Validation Rules

### HierarchyLevel Steps
- `levelsUp` must be ≥ 1
- Multiple HierarchyLevel steps in one definition **must not have overlapping ranges**

### OrgNode Steps
- `orgNodeId` must be set
- `bypassHierarchyCheck: false` means the node must be in the requester's chain
- `bypassHierarchyCheck: true` bypasses the chain check

### DirectEmployee Steps
- `directEmployeeId` must be set
- The employee must be in the same company and active

---

## Dedup and Skip Rules

### Self-Approval Skip
If the requester is a manager at a level, they are **excluded** from approvers at that level.

### Empty Level Skip
If a level has **no managers**, that level is **skipped** entirely.

### Duplicate Approver Skip
If the same person would appear at multiple levels (due to overlapping assignments), they appear only at the **earliest** level and are removed from later ones.

---

## Key Files

| File | Purpose |
|------|---------|
| `WorkflowStepType.cs` | Enum: OrgNode=0, DirectEmployee=1, HierarchyLevel=2 |
| `RequestWorkflow.cs` | Domain model with StartFromLevel and LevelsUp fields |
| `WorkflowResolutionService.cs` | Core algorithm that resolves chains at submission time |
| `PreviewApprovalChainQuery.cs` | Preview endpoint — see chain before submitting |
