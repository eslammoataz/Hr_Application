# bypassHierarchyCheck Explained

## Overview

`bypassHierarchyCheck` is a flag on **OrgNode workflow steps**. It controls whether the referenced org node must be in the requester's upward hierarchy chain, or whether it can be anywhere in the company.

**Only applies to `stepType: 0` (OrgNode) steps.**

---

## The Two Values

### `bypassHierarchyCheck: false` (default)

The referenced org node **must be an ancestor** of the requester's node in the org tree.

```json
{
  "stepType": 0,
  "orgNodeId": "finance-division-id",
  "bypassHierarchyCheck": false,
  "sortOrder": 1
}
```

**Rule:** The requester's upward chain must pass through this node.

---

### `bypassHierarchyCheck: true`

The org node does **not** need to be in the requester's hierarchy. Any manager of that node can approve.

```json
{
  "stepType": 0,
  "orgNodeId": "finance-division-id",
  "bypassHierarchyCheck": true,
  "sortOrder": 1
}
```

**Rule:** Any manager of this node can approve, regardless of where they sit relative to the requester.

---

## Visual Example

```
DemoCo
├── Engineering Division
│   └── Backend Dept        ← Charlie's chain passes through here
│       └── API Team         ← Charlie sits here
│
└── Finance Division        ← Charlie's chain does NOT pass through here
    └── Accounting Dept
        └── Payables Team
```

**Charlie submits a request with:**
```json
{ "stepType": 0, "orgNodeId": "finance-division-id", "bypassHierarchyCheck": false }
```

**Result:** ❌ Request rejected — Finance Division is not in Charlie's upward chain.

**Charlie submits with:**
```json
{ "stepType": 0, "orgNodeId": "finance-division-id", "bypassHierarchyCheck": true }
```

**Result:** ✅ Request accepted — Finance Division managers can approve Charlie.

---

## When to Use Each

| Scenario | bypassHierarchyCheck |
|----------|:---:|
| "Your direct department manager must approve" | `false` |
| "Your upward chain up to CEO must approve" | `false` |
| "Finance department must review ALL requests" | `true` |
| "HR must approve ALL resignations regardless of who submits" | `true` |
| "IT must approve ALL asset requests from any department" | `true` |
| "CEO office must approve ALL end-of-service clearances" | `true` |

---

## Common Patterns

### Pattern 1: Upward-Only Chain (bypass=false)

Use when the approval must follow the management hierarchy upward.

```json
"steps": [
  { "stepType": 0, "orgNodeId": "backend-dept-id", "bypassHierarchyCheck": false, "sortOrder": 1 },
  { "stepType": 0, "orgNodeId": "engineering-div-id", "bypassHierarchyCheck": false, "sortOrder": 2 }
]
```

Only employees whose chain passes through Backend Dept → Engineering Div can submit this request.

---

### Pattern 2: Policy Department (bypass=true)

Use when a specific department must review requests company-wide.

```json
"steps": [
  { "stepType": 0, "orgNodeId": "finance-div-id", "bypassHierarchyCheck": true, "sortOrder": 1 }
]
```

Any employee in any department can submit. Finance managers approve.

---

### Pattern 3: Mixed

```json
"steps": [
  { "stepType": 0, "orgNodeId": "hr-div-id", "bypassHierarchyCheck": true, "sortOrder": 1 },
  { "stepType": 0, "orgNodeId": "ceo-office-id", "bypassHierarchyCheck": true, "sortOrder": 2 }
]
```

All requests go through HR first (regardless of who submitted), then to CEO office.

---

## Summary

| bypassHierarchyCheck | Meaning | Use When |
|:---:|---------|----------|
| `false` | Node must be in requester's chain | The approver is part of the requester's management chain |
| `true` | Node can be anywhere in company | A policy/review department that handles all requests |
