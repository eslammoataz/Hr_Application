# PLAN — Approval Chain Display: StepLabel + ApprovalMode

> **Purpose for the implementing model:** Follow this plan top-to-bottom. Do NOT improvise field names or locations. Every code snippet in this plan is copy-paste ready. When editing an existing file, preserve ALL existing code — only ADD the new pieces unless the instruction explicitly says "replace" or "rewrite full file."
>
> **CRITICAL — no truncation:** If a file is long, complete it fully. Do NOT end mid-statement. Re-read the file after writing it to verify the last lines are complete (balanced braces, final `}` for class and namespace).

---

## 1. Problem Statement

Today, when a requester views their approval chain and a step has multiple eligible approvers (e.g. a `CompanyRole` step with 5 HR role holders, or an `OrgNode` step with 3 managers), the UI has no way to know this is an **OR gate** (any one approver suffices) vs an **AND gate** (all must approve). Requesters see 5 names and assume all 5 must sign off.

**Root cause:** `PlannedStepDto` exposes a flat `Approvers` list without an explicit approval-semantics marker or a primary display label for the step.

**Fix:** add two fields to `PlannedStepDto` so the API expresses the semantics clearly:
1. `StepLabel` — the primary headline for the step (role name / employee name / org node name).
2. `ApprovalMode` — enum (`AnyOf` today; room for `All`, `Quorum`, etc. later).

The `Approvers` list stays as-is (for transparency / expand-to-see-who UX).

---

## 2. Locked Design Decisions

These decisions are fixed. Do NOT second-guess.

**D1.** `ApprovalMode` is an **enum**, not a bool. Values: `AnyOf = 0`. No other values today, but the enum shape is required so we can extend later without breaking clients.

**D2.** `StepLabel` is a **non-nullable string**, defaulting to `""`. It is set explicitly in `BuildApprovalChainAsync` for every step type. UIs can fall back to `NodeName` / `RoleName` if `StepLabel` is empty (this handles old snapshots).

**D3.** `StepLabel` format per step type (exact strings):
- `OrgNode` → the node name, e.g. `"Engineering Division"`
- `DirectEmployee` → the employee full name, e.g. `"Sarah Johnson"`
- `HierarchyLevel` → `"{NodeName} (Level {level})"`, e.g. `"Backend Dept (Level 2)"`
- `CompanyRole` → the role name, e.g. `"HR Reviewer"`

**D4.** `ApprovalMode` is **always set to `AnyOf`** for every step type today. (Even DirectEmployee, which has 1 approver, is logically AnyOf-of-1.) Do NOT branch on step type to set a different value. The enum default is also AnyOf, so backward compat with old JSON snapshots is automatic.

**D5.** `ApproverCount` is a **computed, read-only property** (`=> Approvers.Count`). It is serialized but not settable. This keeps callers from getting out of sync.

**D6.** Existing fields on `PlannedStepDto` (`NodeId`, `NodeName`, `CompanyRoleId`, `RoleName`, `ResolvedFromLevel`, `SortOrder`, `Approvers`, `StepType`) remain **unchanged**. Do NOT remove or rename anything. This is additive.

**D7.** No database migration needed. `PlannedStepsJson` is already a `string` (JSON blob). New fields will appear in new submissions automatically. Old snapshots deserialize with `StepLabel = ""` and `ApprovalMode = AnyOf` (enum default), and UI falls back to `NodeName`.

**D8.** No changes to step-level APIs (ApproveRequestCommand, RejectRequestCommand, etc.). Approver-in-list check stays exactly as-is.

**D9.** The `PreviewApprovalChainQuery` and `GetRequestDefinitions` endpoints already return `PlannedStepDto` (preview) / `WorkflowStepDto` (definitions). The preview endpoint automatically picks up the new fields — no handler changes needed. Definitions are not affected by this plan.

---

## 3. Files to Modify / Create

| Action | File |
|---|---|
| **CREATE** | `HrSystemApp.Domain/Enums/ApprovalMode.cs` |
| **MODIFY** | `HrSystemApp.Application/DTOs/Requests/PlannedStepDto.cs` |
| **MODIFY** | `HrSystemApp.Infrastructure/Services/WorkflowResolutionService.cs` |
| **MODIFY (tests)** | `tests/HrSystemApp.Tests.Unit/Services/WorkflowResolutionServiceHierarchyTests.cs` |
| **MODIFY (tests, if exists)** | `tests/HrSystemApp.Tests.Unit/Services/WorkflowResolutionServiceRoleTests.cs` (if it exists; skip otherwise) |

No other files should be touched.

---

## 4. Step-by-Step Implementation

### Step 4.1 — Create the `ApprovalMode` enum

**File:** `HrSystemApp.Domain/Enums/ApprovalMode.cs` **(CREATE NEW)**

Write this file in full, exactly as shown:

```csharp
namespace HrSystemApp.Domain.Enums;

/// <summary>
/// Governs how approvers within a single approval step combine.
/// Currently only AnyOf is supported; the enum shape is reserved for future modes
/// (All / unanimous, Quorum(N), Majority).
/// </summary>
public enum ApprovalMode
{
    /// <summary>
    /// Any single approver in the step's Approvers list can approve or reject.
    /// First action wins and advances (or rejects) the step.
    /// </summary>
    AnyOf = 0
}
```

That is the entire file. 13 lines. Close the namespace with `}` is NOT needed — C# file-scoped namespaces do not require closing braces. Save, stop.

---

### Step 4.2 — Extend `PlannedStepDto`

**File:** `HrSystemApp.Application/DTOs/Requests/PlannedStepDto.cs`

**IMPORTANT — read this first:** at the time of this plan, the file on disk is truncated (only 22 lines, ends mid-property inside `WorkflowStepDto`). Before doing this step, ensure the full `PlannedStepDto.cs` file exists in its complete form per the shape below. If the file is truncated, rewrite it in full using this canonical structure:

```csharp
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.Requests;

/// <summary>
/// Request-definition-level step shape. Used by admin create/update endpoints
/// and mapped to/from the RequestWorkflowStep entity.
/// </summary>
public class WorkflowStepDto
{
    public WorkflowStepType StepType { get; set; } = WorkflowStepType.OrgNode;

    /// <summary>
    /// Human-readable name of the step type: "OrgNode", "DirectEmployee", "HierarchyLevel", or "CompanyRole".
    /// </summary>
    public string StepTypeName { get; set; } = string.Empty;

    public Guid? OrgNodeId { get; set; }
    public bool BypassHierarchyCheck { get; set; } = false;
    public Guid? DirectEmployeeId { get; set; }

    /// <summary>For HierarchyLevel steps: first ancestor level to include (1-based). Defaults to 1.</summary>
    public int? StartFromLevel { get; set; }

    /// <summary>For HierarchyLevel steps: number of consecutive levels to expand. Required for HierarchyLevel.</summary>
    public int? LevelsUp { get; set; }

    /// <summary>For CompanyRole steps: the role to resolve approvers from.</summary>
    public Guid? CompanyRoleId { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>
/// A snapshotted step in the approval chain, stored as JSON on the Request at submission time.
/// Immutable after creation — changes to org structure do not retroactively affect in-flight requests.
/// </summary>
public class PlannedStepDto
{
    public WorkflowStepType StepType { get; set; }

    /// <summary>For OrgNode / HierarchyLevel steps: the OrgNode ID.</summary>
    public Guid? NodeId { get; set; }

    /// <summary>Display name for the step (OrgNode name, employee name, or role name).</summary>
    public string NodeName { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>For HierarchyLevel steps: the ancestor level this was resolved from.</summary>
    public int? ResolvedFromLevel { get; set; }

    /// <summary>For CompanyRole steps: the role ID.</summary>
    public Guid? CompanyRoleId { get; set; }

    /// <summary>For CompanyRole steps: the role name (display).</summary>
    public string? RoleName { get; set; }

    /// <summary>
    /// Primary headline shown to users for this step (one of: node name, employee name,
    /// "NodeName (Level N)", or role name). Set by WorkflowResolutionService.
    /// </summary>
    public string StepLabel { get; set; } = string.Empty;

    /// <summary>
    /// Semantics of how <see cref="Approvers"/> combine. Today always AnyOf (any single approver suffices).
    /// </summary>
    public ApprovalMode ApprovalMode { get; set; } = ApprovalMode.AnyOf;

    public List<ApproverDto> Approvers { get; set; } = new();

    /// <summary>Convenience: number of eligible approvers on this step.</summary>
    public int ApproverCount => Approvers.Count;
}

public class ApproverDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
}
```

Verify the final lines of the file are `}` for `ApproverDto` and nothing after. Count: 3 classes — `WorkflowStepDto`, `PlannedStepDto`, `ApproverDto`. All must be in this one file.

**If the file on disk already has the three classes intact (not truncated), do NOT rewrite it — only make two additions to `PlannedStepDto`:**

1. Add the `StepLabel` property right before the `Approvers` property:
   ```csharp
   public string StepLabel { get; set; } = string.Empty;
   public ApprovalMode ApprovalMode { get; set; } = ApprovalMode.AnyOf;
   ```
2. Add the `ApproverCount` computed property right after the `Approvers` property:
   ```csharp
   public int ApproverCount => Approvers.Count;
   ```

Ensure the `using HrSystemApp.Domain.Enums;` at the top is present.

---

### Step 4.3 — Set `StepLabel` in `WorkflowResolutionService`

**File:** `HrSystemApp.Infrastructure/Services/WorkflowResolutionService.cs`

**IMPORTANT — read this first:** at the time of this plan, this file is truncated on disk (ends at line 218/219 mid-statement in the OrgNode branch: `if (step.OrgNod`). Before doing this step, the file must be completed. If you find the file truncated, complete it first per the logic that was there before (OrgNode branch: resolve node, skip if self, check hierarchy, build approvers from `GetManagersByNodeAsync`, dedup, add PlannedStepDto, then close the branch; after the foreach, renumber SortOrder 1..N, check empty chain, return `Result.Success(plannedSteps)`). Reuse the same patterns as the HierarchyLevel and CompanyRole branches already present in lines 92–192.

Once the file is complete and builds, make the following four edits. Each edit adds `StepLabel` (and confirms `ApprovalMode` — though since that defaults to `AnyOf` in the DTO, you can omit the explicit assignment). For clarity and explicitness, set both on every branch.

#### 4.3.a — DirectEmployee branch

Find the block (around lines 80–90) where `plannedSteps.Add(new PlannedStepDto { ... })` is called in the DirectEmployee branch. Add `StepLabel` and `ApprovalMode`:

**Before:**
```csharp
plannedSteps.Add(new PlannedStepDto
{
    StepType = WorkflowStepType.DirectEmployee,
    NodeId = null,
    NodeName = directEmployee.FullName,
    SortOrder = 0, // renumbered at the end
    Approvers = new List<ApproverDto>
    {
        new ApproverDto { EmployeeId = directEmployee.Id, EmployeeName = directEmployee.FullName }
    }
});
```

**After:**
```csharp
plannedSteps.Add(new PlannedStepDto
{
    StepType = WorkflowStepType.DirectEmployee,
    NodeId = null,
    NodeName = directEmployee.FullName,
    StepLabel = directEmployee.FullName,
    ApprovalMode = ApprovalMode.AnyOf,
    SortOrder = 0, // renumbered at the end
    Approvers = new List<ApproverDto>
    {
        new ApproverDto { EmployeeId = directEmployee.Id, EmployeeName = directEmployee.FullName }
    }
});
```

#### 4.3.b — HierarchyLevel branch

Find the block (around lines 143–151) where `plannedSteps.Add(new PlannedStepDto { ... })` is called inside the `for (int level = ...)` loop. Add `StepLabel` and `ApprovalMode`:

**Before:**
```csharp
plannedSteps.Add(new PlannedStepDto
{
    StepType = WorkflowStepType.HierarchyLevel,
    NodeId = node.Id,
    NodeName = node.Name,
    SortOrder = 0, // renumbered at the end
    Approvers = approvers,
    ResolvedFromLevel = level
});
```

**After:**
```csharp
plannedSteps.Add(new PlannedStepDto
{
    StepType = WorkflowStepType.HierarchyLevel,
    NodeId = node.Id,
    NodeName = node.Name,
    StepLabel = $"{node.Name} (Level {level})",
    ApprovalMode = ApprovalMode.AnyOf,
    SortOrder = 0, // renumbered at the end
    Approvers = approvers,
    ResolvedFromLevel = level
});
```

The string format must be exactly `"{node.Name} (Level {level})"` — space between name and `(`, capital `L` in `Level`, space between `Level` and the number.

#### 4.3.c — CompanyRole branch

Find the block (around lines 182–191). Add `StepLabel` and `ApprovalMode`:

**Before:**
```csharp
plannedSteps.Add(new PlannedStepDto
{
    StepType = WorkflowStepType.CompanyRole,
    NodeId = null,
    NodeName = role.Name,
    CompanyRoleId = role.Id,
    RoleName = role.Name,
    SortOrder = 0,
    Approvers = approvers
});
```

**After:**
```csharp
plannedSteps.Add(new PlannedStepDto
{
    StepType = WorkflowStepType.CompanyRole,
    NodeId = null,
    NodeName = role.Name,
    CompanyRoleId = role.Id,
    RoleName = role.Name,
    StepLabel = role.Name,
    ApprovalMode = ApprovalMode.AnyOf,
    SortOrder = 0,
    Approvers = approvers
});
```

#### 4.3.d — OrgNode branch

Find the `plannedSteps.Add(new PlannedStepDto { ... })` call inside the OrgNode branch (after the hierarchy-validation code, after `stepNode` is resolved). Add `StepLabel` and `ApprovalMode`:

**Target shape:**
```csharp
plannedSteps.Add(new PlannedStepDto
{
    StepType = WorkflowStepType.OrgNode,
    NodeId = stepNode.Id,
    NodeName = stepNode.Name,
    StepLabel = stepNode.Name,
    ApprovalMode = ApprovalMode.AnyOf,
    SortOrder = 0, // renumbered at the end
    Approvers = approvers
});
```

Adapt variable names to whatever the current code uses in that branch (e.g. if it's `node` instead of `stepNode`, use that). The two added lines are `StepLabel = <nodeName>,` and `ApprovalMode = ApprovalMode.AnyOf,`.

#### 4.3.e — Add `using` if missing

Ensure the file has:
```csharp
using HrSystemApp.Domain.Enums;
```
It likely does already (for `WorkflowStepType`). Verify `ApprovalMode` resolves from that namespace.

---

### Step 4.4 — Update existing tests (if they break)

**File:** `tests/HrSystemApp.Tests.Unit/Services/WorkflowResolutionServiceHierarchyTests.cs`

Existing assertions that compare the count / shape of `plannedSteps` entries will still pass. Only tests that use *object equality* on the full DTO would break. If any test fails with a message about `StepLabel` or `ApprovalMode`, update that specific assertion by adding the expected `StepLabel` per D3 format. Do NOT rewrite unaffected tests.

Add ONE new test to verify the `HierarchyLevel` label format:

```csharp
[Fact]
public async Task BuildApprovalChain_HierarchyLevel_SetsStepLabelWithLevel()
{
    // Arrange: use an existing test scaffold with a 2-level hierarchy and
    // a single HierarchyLevel step { StartFromLevel = 1, LevelsUp = 2 }.
    // Requester has an immediate parent node "Backend Dept" with a manager.
    // [Reuse the same setup used by existing HierarchyLevel tests in this file.]

    // Act
    var result = await _service.BuildApprovalChainAsync(requesterId, requesterNodeId, steps, default);

    // Assert
    result.IsSuccess.Should().BeTrue();
    var plannedSteps = result.Value;
    plannedSteps.Should().NotBeEmpty();
    plannedSteps[0].StepLabel.Should().MatchRegex(@".+ \(Level \d+\)$");
    plannedSteps[0].ApprovalMode.Should().Be(ApprovalMode.AnyOf);
}
```

Place the test alongside existing HierarchyLevel tests. Reuse existing mock setup rather than building new mocks.

Add ONE new test for `ApproverCount`:

```csharp
[Fact]
public void PlannedStepDto_ApproverCount_MatchesApproversListSize()
{
    // Arrange
    var dto = new PlannedStepDto
    {
        Approvers = new List<ApproverDto>
        {
            new() { EmployeeId = Guid.NewGuid(), EmployeeName = "A" },
            new() { EmployeeId = Guid.NewGuid(), EmployeeName = "B" },
            new() { EmployeeId = Guid.NewGuid(), EmployeeName = "C" }
        }
    };

    // Assert
    dto.ApproverCount.Should().Be(3);
}
```

---

## 5. Verification Checklist

After implementing, verify each of the following. Do NOT mark complete until every item passes.

1. [ ] `HrSystemApp.Domain/Enums/ApprovalMode.cs` exists and contains only `AnyOf = 0`.
2. [ ] `PlannedStepDto` has three new members: `StepLabel` (string, default `""`), `ApprovalMode` (enum, default `AnyOf`), `ApproverCount` (computed get-only `int`).
3. [ ] `PlannedStepDto.cs` file is not truncated — the last non-blank line is `}` closing `ApproverDto`.
4. [ ] `WorkflowResolutionService.cs` file is not truncated — the method closes with `return Result.Success(plannedSteps);` (or similar) and the class+namespace braces are balanced.
5. [ ] Run `dotnet build HrSystemApp.sln` — succeeds with **zero** errors.
6. [ ] All four branches in `BuildApprovalChainAsync` set both `StepLabel` and `ApprovalMode = ApprovalMode.AnyOf`.
7. [ ] `StepLabel` format matches D3 exactly:
   - OrgNode: node name only (e.g. `"Engineering Division"`)
   - DirectEmployee: employee full name (e.g. `"Sarah Johnson"`)
   - HierarchyLevel: `"{nodeName} (Level {level})"` — verify capital L and parentheses
   - CompanyRole: role name only (e.g. `"HR Reviewer"`)
8. [ ] Run `dotnet test tests/HrSystemApp.Tests.Unit` — all tests pass (including the two new ones).
9. [ ] Submit a test request via the API (OrgNode chain). Inspect the returned `PlannedSteps` — every entry has `stepLabel` populated and `approvalMode` = `"AnyOf"` (or `0`).
10. [ ] Submit a test request with a `CompanyRole` step where 3+ employees hold the role. Verify the response has ONE step with `stepLabel = "<role name>"`, `approvalMode = "AnyOf"`, and `approvers` list containing all 3.
11. [ ] Submit a `HierarchyLevel` test request. Verify `stepLabel` matches `"<node> (Level N)"`.
12. [ ] Hit `GET /api/request-definitions/preview-approval-chain` — response serializes the new fields.
13. [ ] Deserialize an OLD `PlannedStepsJson` snapshot (one created before this change) from the DB. Verify it deserializes without error, `StepLabel` is `""` (empty), `ApprovalMode` is `AnyOf` (default).
14. [ ] No changes made to: any migration, any controller, any command handler, the frontend, or any file under `HrSystemApp.Api/Authorization/`.
15. [ ] No changes made to `ApproveRequestCommand` or `RejectRequestCommand` — the approver-check logic is untouched.

---

## 6. What This Plan Does NOT Do

Explicitly out of scope. Do not include:

- No frontend work.
- No new API endpoints.
- No changes to the approval check logic (who can approve).
- No `All` / `Quorum` / `Majority` approval modes — only `AnyOf` today.
- No retroactive backfill of `StepLabel` onto existing in-flight requests' `PlannedStepsJson`. Old snapshots stay as-is; new requests get the new fields.
- No changes to `WorkflowStepDto` (the admin-facing definition DTO) — only `PlannedStepDto` (the snapshot DTO).
- No documentation updates.

---

## 7. Pre-Implementation Sanity Check

Before writing a single line of code:

1. Run `dotnet build` on the current state. If it does NOT build, the truncation issues in `PlannedStepDto.cs` and `WorkflowResolutionService.cs` must be repaired first (per the "IMPORTANT" notes in 4.2 and 4.3). Do NOT proceed until `dotnet build` is clean.
2. Confirm `HrSystemApp.Domain.Enums.WorkflowStepType` contains `CompanyRole = 3`. If not, this plan's assumptions are invalid — stop and raise.
3. Confirm `tests/HrSystemApp.Tests.Unit/Services/WorkflowResolutionServiceHierarchyTests.cs` exists and currently passes.

Only after those three checks are green, proceed with sections 4.1 → 4.4 in order.

---

## 8. Summary for the Implementer

You are adding two new fields and one computed property to one DTO, setting them in four places in one service, and writing two small tests. Expected change: ~15 lines added across 3 files (plus optionally a full rewrite of `PlannedStepDto.cs` and `WorkflowResolutionService.cs` if they are truncated). No behavior changes. No DB changes. No API endpoint changes.

If you find yourself modifying any file not listed in Section 3, STOP — you are outside the plan.
