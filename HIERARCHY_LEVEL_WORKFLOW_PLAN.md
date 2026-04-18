# Implementation Plan: Dynamic Hierarchy-Based Approval Chains (`HierarchyLevel` step type)

> **Audience:** The engineer (or model) implementing this feature. Follow the phases in order. Every file path is absolute from the repository root. Every code snippet is copy-paste ready unless marked otherwise.

---

## 1. Context

### The problem
Today, a `RequestDefinition` has `RequestWorkflowStep`s of two types:
- `OrgNode` — hardcodes a specific org node; its managers approve.
- `DirectEmployee` — hardcodes a specific employee.

Because nodes are hardcoded, a single definition cannot serve employees in different branches of the org tree. Alex (in "Backend Team") and Bob (in "Frontend Team") would need different definitions even though the business rule is the same ("my team lead → my dept manager → my VP").

### The solution
Add a third step type — `HierarchyLevel` — whose chain is resolved **dynamically at submission time** based on the requester's position in the org tree. The admin specifies **which levels** to include (`StartFromLevel` and `LevelsUp`); the system resolves them per-requester.

### The three business use cases this enables

1. **Pure hierarchy chain.** One step with `levelsUp=3` gives: requester's own-node manager → +1 level → +2 levels.
2. **Pure direct employee.** One step pointing at a fixed person (already supported today, no change needed).
3. **Mixed / split chain.** Combine everything: e.g., `HierarchyLevel (levels 1–2)` → `DirectEmployee (Special Person)` → `HierarchyLevel (level 3)`. This lets an admin insert a mandatory fixed approver between hierarchy levels.

### Key design decisions (already made — do not re-litigate)

| # | Decision | Rationale |
|---|---|---|
| D1 | **Level 1 = the requester's OWN node.** Level 2 = immediate parent. Level 3 = grandparent. Etc. | Matches the user's mental model of "team lead approves first." |
| D2 | **Multiple `HierarchyLevel` steps allowed** in one definition, but their level ranges **must not overlap.** | Enables use case 3. Overlap would cause duplicate approvers. |
| D3 | **Gaps between ranges are allowed** (e.g., step covering level 1, then DirectEmployee, then step covering level 3 — level 2 skipped). | Gives admins flexibility. |
| D4 | **If a resolved level has zero managers**, skip that level and continue up. | Lenient — don't fail a request because one ancestor node has no manager set. |
| D5 | **Self-approval skip** at every resolved level. Same rule as existing `OrgNode` steps. | Consistent with current behavior. |
| D6 | **Dedup across the full chain**: if the same employee appears as an approver at multiple resolved levels, keep them **only at the earliest level**. If that empties a later level, drop the level. | Prevents one person approving the same request twice. |
| D7 | **Snapshot frozen at submission time.** `Request.PlannedStepsJson` does not re-resolve if the org tree changes later. | Already how the system works; just preserve it. |
| D8 | **`StartFromLevel` defaults to 1** when omitted — so `{levelsUp: 3}` means levels 1–3. | Keeps the simple case simple. |
| D9 | **Preview endpoint** accepts either a saved `DefinitionId` or inline `Steps`. Any authenticated user can call it. | Supports "show me my chain before I submit" UX; admin authoring preview. |

---

## 2. Prerequisites — read these before coding

Read (not modify) these files first to understand the existing structure. File paths are absolute from the repo root.

- `HrSystemApp.Domain/Enums/WorkflowStepType.cs` — current enum has 2 values.
- `HrSystemApp.Domain/Models/RequestWorkflow.cs` — current `RequestWorkflowStep` shape.
- `HrSystemApp.Application/DTOs/Requests/PlannedStepDto.cs` — current DTOs.
- `HrSystemApp.Infrastructure/Services/WorkflowResolutionService.cs` — current resolution logic. The new `HierarchyLevel` branch plugs into the `foreach` loop.
- `HrSystemApp.Application/Features/Requests/Commands/Admin/CreateRequestDefinitionCommand.cs` — current validation pattern.
- `HrSystemApp.Application/Features/Requests/Commands/Admin/UpdateRequestDefinitionCommand.cs` — mirror of the create command.
- `HrSystemApp.Application/Features/Requests/Commands/CreateRequest/CreateRequestCommand.cs` — where `BuildApprovalChainAsync` gets called at submission time.
- `HrSystemApp.Infrastructure/Repositories/OrgNodeRepository.cs` — `GetAncestorsAsync` returns ancestors **ordered immediate-parent-first** (this matters, do not re-sort).
- `HrSystemApp.Api/Controllers/RequestDefinitionsController.cs` — where the new `POST /preview` endpoint lives.
- `HrSystemApp.Application/Errors/DomainErrors.cs` — existing error catalog; you will add two new errors.

### Codebase conventions to respect

- **SortOrder is 1-based** (not 0-based). All existing code assumes this.
- **Result<T>** pattern for command/query returns: `Result.Success(value)` / `Result.Failure<T>(DomainErrors.X.Y)`.
- **MediatR** for commands and queries. Each feature has `Command/Query + Handler` in the same file or adjacent files.
- **EF Core migrations**: folder is `HrSystemApp.Infrastructure/Migrations/`. Naming: `{YYYYMMddHHmmss}_Description.cs`. Use `dotnet ef migrations add` from the solution root.
- **Tests**: xUnit + Moq + FluentAssertions. Tests live under `tests/HrSystemApp.Tests.Unit/`.
- **No `Depth` property on `OrgNode`.** The tree is an adjacency list (`ParentId` only). Do not reference `OrgNode.Depth` — it doesn't exist.

---

## 3. Phase-by-phase implementation

Each phase lists the exact files to change and the exact code to write. Do phases in order — later phases depend on earlier ones.

### Phase 1 — Domain model: add enum value and fields

#### Step 1.1 — Add `HierarchyLevel` to the enum

**File:** `HrSystemApp.Domain/Enums/WorkflowStepType.cs`

**Replace the entire file contents with:**

```csharp
namespace HrSystemApp.Domain.Enums;

public enum WorkflowStepType
{
    OrgNode = 0,
    DirectEmployee = 1,
    HierarchyLevel = 2
}
```

#### Step 1.2 — Add `StartFromLevel` and `LevelsUp` to `RequestWorkflowStep`

**File:** `HrSystemApp.Domain/Models/RequestWorkflow.cs`

**Find the `RequestWorkflowStep` class. After the `SortOrder` property (currently ending at the `// Navigation` comment line), insert these two new properties before the navigation section.**

Add the following lines:

```csharp
    /// <summary>
    /// For HierarchyLevel steps: the first ancestor level to include (1-based).
    /// Level 1 = requester's own node. Level 2 = immediate parent. Etc.
    /// NULL for non-HierarchyLevel steps. Defaults to 1 if omitted on a HierarchyLevel step.
    /// </summary>
    public int? StartFromLevel { get; set; }

    /// <summary>
    /// For HierarchyLevel steps: how many consecutive levels this step covers starting at StartFromLevel.
    /// NULL for non-HierarchyLevel steps. Required (>= 1) for HierarchyLevel steps.
    /// </summary>
    public int? LevelsUp { get; set; }
```

**Success criteria:** `dotnet build` succeeds with no errors.

---

### Phase 2 — DTOs: expose the new fields

#### Step 2.1 — Extend `WorkflowStepDto` and `PlannedStepDto`

**File:** `HrSystemApp.Application/DTOs/Requests/PlannedStepDto.cs`

**Replace the entire file contents with:**

```csharp
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.DTOs.Requests;

public class WorkflowStepDto
{
    public WorkflowStepType StepType { get; set; } = WorkflowStepType.OrgNode;
    public Guid? OrgNodeId { get; set; }
    public bool BypassHierarchyCheck { get; set; } = false;
    public Guid? DirectEmployeeId { get; set; }

    /// <summary>
    /// For HierarchyLevel steps: first ancestor level to include (1-based). Null on other step types.
    /// Defaults to 1 when omitted on a HierarchyLevel step.
    /// </summary>
    public int? StartFromLevel { get; set; }

    /// <summary>
    /// For HierarchyLevel steps: how many consecutive levels covered. Required (>=1) for HierarchyLevel. Null on other step types.
    /// </summary>
    public int? LevelsUp { get; set; }

    public int SortOrder { get; set; }
}

public class PlannedStepDto
{
    public WorkflowStepType StepType { get; set; } = WorkflowStepType.OrgNode;
    public Guid? NodeId { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<ApproverDto> Approvers { get; set; } = new();

    /// <summary>
    /// For planned steps produced by a HierarchyLevel definition step:
    /// which ancestor level (1-based) this resolved step came from. Null for OrgNode/DirectEmployee steps.
    /// </summary>
    public int? ResolvedFromLevel { get; set; }
}

public class ApproverDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
}
```

#### Step 2.2 — Extend `RequestDefinitionDto` mapping

**File:** `HrSystemApp.Application/Features/Requests/Queries/GetRequestDefinitions/GetRequestDefinitionsQuery.cs`

**Find the block starting with `Steps = d.WorkflowSteps.Select(s => new WorkflowStepDto`. Replace the selector body** (approximately lines 70–77) **with this expanded version:**

```csharp
            Steps = d.WorkflowSteps.Select(s => new WorkflowStepDto
            {
                StepType = s.StepType,
                OrgNodeId = s.OrgNodeId,
                BypassHierarchyCheck = s.BypassHierarchyCheck,
                DirectEmployeeId = s.DirectEmployeeId,
                StartFromLevel = s.StartFromLevel,
                LevelsUp = s.LevelsUp,
                SortOrder = s.SortOrder
            }).ToList(),
```

**Success criteria:** `dotnet build` succeeds. Existing `GET /api/requestdefinitions` still works; `StartFromLevel` and `LevelsUp` now surface in the JSON response (null for existing steps).

---

### Phase 3 — EF configuration and database migration

#### Step 3.1 — Register the new columns in EF configuration

**File:** `HrSystemApp.Infrastructure/Data/Configurations/RequestRelatedConfigurations.cs`

**Find the method `Configure(EntityTypeBuilder<RequestWorkflowStep> builder)`. After the existing `builder.Property(x => x.BypassHierarchyCheck)...HasDefaultValue(false);` block, insert:**

```csharp
        builder.Property(x => x.StartFromLevel)
            .IsRequired(false);

        builder.Property(x => x.LevelsUp)
            .IsRequired(false);
```

#### Step 3.2 — Create the migration

**From the repo root, run:**

```bash
dotnet ef migrations add AddHierarchyLevelWorkflowStep \
  --project HrSystemApp.Infrastructure \
  --startup-project HrSystemApp.Api
```

This generates a new file under `HrSystemApp.Infrastructure/Migrations/`. The migration must:

- Add nullable `StartFromLevel` (int) column to `RequestWorkflowSteps`.
- Add nullable `LevelsUp` (int) column to `RequestWorkflowSteps`.
- Provide a reversible `Down()` that drops both columns.

**If the auto-generated `Up()` is not as expected, replace its body with:**

```csharp
migrationBuilder.AddColumn<int>(
    name: "StartFromLevel",
    table: "RequestWorkflowSteps",
    type: "integer",
    nullable: true);

migrationBuilder.AddColumn<int>(
    name: "LevelsUp",
    table: "RequestWorkflowSteps",
    type: "integer",
    nullable: true);
```

**And the `Down()`:**

```csharp
migrationBuilder.DropColumn(
    name: "LevelsUp",
    table: "RequestWorkflowSteps");

migrationBuilder.DropColumn(
    name: "StartFromLevel",
    table: "RequestWorkflowSteps");
```

#### Step 3.3 — Apply the migration locally

```bash
dotnet ef database update \
  --project HrSystemApp.Infrastructure \
  --startup-project HrSystemApp.Api
```

**Success criteria:** Migration applies without error. Inspect the `RequestWorkflowSteps` table — it should now have `StartFromLevel` and `LevelsUp` nullable int columns. All existing rows have NULL in both (correct — they are not HierarchyLevel steps).

---

### Phase 4 — Add new domain errors

**File:** `HrSystemApp.Application/Errors/DomainErrors.cs`

**Find the `public static class Request` block. Inside it (before the closing brace), append these new errors:**

```csharp
        public static readonly Error MissingLevelsUp = new(
            "Request.MissingLevelsUp", "HierarchyLevel step must have LevelsUp >= 1.");

        public static readonly Error InvalidStartFromLevel = new(
            "Request.InvalidStartFromLevel", "HierarchyLevel step StartFromLevel must be >= 1 when specified.");

        public static readonly Error HierarchyRangesOverlap = new(
            "Request.HierarchyRangesOverlap", "Two HierarchyLevel steps in this definition cover overlapping levels.");

        public static readonly Error HierarchyLevelFieldsOnNonHierarchyStep = new(
            "Request.HierarchyLevelFieldsOnNonHierarchyStep", "StartFromLevel/LevelsUp are only valid on HierarchyLevel steps.");

        public static readonly Error UnexpectedFieldsOnHierarchyLevelStep = new(
            "Request.UnexpectedFieldsOnHierarchyLevelStep", "HierarchyLevel steps must not have OrgNodeId, DirectEmployeeId, or BypassHierarchyCheck set.");
```

**Success criteria:** `dotnet build` succeeds.

---

### Phase 5 — Validation in Create / Update command handlers

The Create and Update handlers duplicate validation logic today. Add the new rules to **both** handlers identically. Do not refactor them into a shared helper in this PR — keep the change footprint minimal.

#### Step 5.1 — Validation rules to add

Both handlers will get the same three new validation blocks, inserted **immediately after** the existing "Validate steps have unique sort orders" block (section 2 in Create, section 3 in Update), and **before** the existing per-step loop.

**Validation block A — per-step field consistency:**

```csharp
        // (NEW) Per-step field consistency
        foreach (var step in request.Steps)
        {
            if (step.StepType == WorkflowStepType.HierarchyLevel)
            {
                // HierarchyLevel must have LevelsUp >= 1
                if (!step.LevelsUp.HasValue || step.LevelsUp.Value < 1)
                    return Result.Failure<Guid>(DomainErrors.Request.MissingLevelsUp);

                // StartFromLevel (if set) must be >= 1
                if (step.StartFromLevel.HasValue && step.StartFromLevel.Value < 1)
                    return Result.Failure<Guid>(DomainErrors.Request.InvalidStartFromLevel);

                // HierarchyLevel must NOT have OrgNodeId, DirectEmployeeId, or BypassHierarchyCheck
                if (step.OrgNodeId.HasValue || step.DirectEmployeeId.HasValue || step.BypassHierarchyCheck)
                    return Result.Failure<Guid>(DomainErrors.Request.UnexpectedFieldsOnHierarchyLevelStep);
            }
            else
            {
                // OrgNode and DirectEmployee steps must NOT have StartFromLevel or LevelsUp
                if (step.StartFromLevel.HasValue || step.LevelsUp.HasValue)
                    return Result.Failure<Guid>(DomainErrors.Request.HierarchyLevelFieldsOnNonHierarchyStep);
            }
        }
```

**Validation block B — non-overlapping HierarchyLevel ranges:**

```csharp
        // (NEW) HierarchyLevel ranges must not overlap
        var hierarchyRanges = request.Steps
            .Where(s => s.StepType == WorkflowStepType.HierarchyLevel)
            .Select(s => new
            {
                Start = s.StartFromLevel ?? 1,
                End = (s.StartFromLevel ?? 1) + s.LevelsUp!.Value - 1,
                s.SortOrder
            })
            .ToList();

        for (int i = 0; i < hierarchyRanges.Count; i++)
        {
            for (int j = i + 1; j < hierarchyRanges.Count; j++)
            {
                var a = hierarchyRanges[i];
                var b = hierarchyRanges[j];
                // Overlap test: max(start) <= min(end)
                if (Math.Max(a.Start, b.Start) <= Math.Min(a.End, b.End))
                {
                    _logger.LogWarning("HierarchyLevel ranges overlap between steps sortOrder {A} [{As}..{Ae}] and {B} [{Bs}..{Be}]",
                        a.SortOrder, a.Start, a.End, b.SortOrder, b.Start, b.End);
                    return Result.Failure<Guid>(DomainErrors.Request.HierarchyRangesOverlap);
                }
            }
        }
```

#### Step 5.2 — Update the existing per-step validation loop

In both `CreateRequestDefinitionCommandHandler.Handle` and `UpdateRequestDefinitionCommandHandler.Handle`, the existing loop validates only `OrgNode` and `DirectEmployee` steps. Add a no-op branch so `HierarchyLevel` steps don't fall through to an error. Find the loop that starts with `foreach (var step in request.Steps)` (after the new validation blocks A and B) and ensure it handles all three types — specifically, the existing `if/else if` for `StepType` should now have no fallthrough for `HierarchyLevel` (they're already validated in block A). You can leave the loop as is; it simply will not enter either branch for HierarchyLevel steps. Verify this by reading the code.

#### Step 5.3 — Update the entity construction

In both handlers, find the block that constructs `new RequestWorkflowStep { ... }` from `request.Steps.Select(...)`. **Extend the projection to include `StartFromLevel` and `LevelsUp`.**

Replace (in both Create and Update handlers):

```csharp
        WorkflowSteps = request.Steps.Select(s => new RequestWorkflowStep
        {
            StepType = s.StepType,
            OrgNodeId = s.StepType == WorkflowStepType.OrgNode ? s.OrgNodeId : null,
            BypassHierarchyCheck = s.StepType == WorkflowStepType.OrgNode && s.BypassHierarchyCheck,
            DirectEmployeeId = s.StepType == WorkflowStepType.DirectEmployee ? s.DirectEmployeeId : null,
            SortOrder = s.SortOrder
        }).ToList()
```

With (note: Update handler version also has `RequestDefinitionId = definition.Id`; keep that line):

```csharp
        WorkflowSteps = request.Steps.Select(s => new RequestWorkflowStep
        {
            StepType = s.StepType,
            OrgNodeId = s.StepType == WorkflowStepType.OrgNode ? s.OrgNodeId : null,
            BypassHierarchyCheck = s.StepType == WorkflowStepType.OrgNode && s.BypassHierarchyCheck,
            DirectEmployeeId = s.StepType == WorkflowStepType.DirectEmployee ? s.DirectEmployeeId : null,
            StartFromLevel = s.StepType == WorkflowStepType.HierarchyLevel ? (s.StartFromLevel ?? 1) : (int?)null,
            LevelsUp = s.StepType == WorkflowStepType.HierarchyLevel ? s.LevelsUp : (int?)null,
            SortOrder = s.SortOrder
        }).ToList()
```

**Success criteria:** `dotnet build` succeeds. A unit test (see Phase 8) that submits a definition with overlapping ranges should fail with `HierarchyRangesOverlap`; a valid definition should succeed.

---

### Phase 6 — Resolution algorithm

This is the core behavioral change. Update `BuildApprovalChainAsync` to handle the `HierarchyLevel` step type, apply dedup, and skip empty levels.

**File:** `HrSystemApp.Infrastructure/Services/WorkflowResolutionService.cs`

**Replace the method `BuildApprovalChainAsync` in its entirety with this new version.** The `ValidateWorkflowStepsAsync` method stays as-is (it's only used for legacy validation and doesn't need to know about HierarchyLevel).

```csharp
    public async Task<Result<List<PlannedStepDto>>> BuildApprovalChainAsync(
        Guid requesterEmployeeId,
        Guid requesterNodeId,
        List<WorkflowStepDto> definitionSteps,
        CancellationToken ct)
    {
        _logger.LogInformation("Building approval chain for employee {EmployeeId} from node {NodeId}",
            requesterEmployeeId, requesterNodeId);

        // Is the requester a manager at their own node? Used for self-approval skip below.
        var isManagerAtOwnNode = await _unitOfWork.OrgNodeAssignments
            .IsManagerAtNodeAsync(requesterEmployeeId, requesterNodeId, ct);

        // Load the requester's own node.
        var ownNode = await _unitOfWork.OrgNodes.GetByIdAsync(requesterNodeId, ct);
        if (ownNode == null)
        {
            _logger.LogWarning("Employee's node {NodeId} not found", requesterNodeId);
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.OrgNode.NotFound);
        }

        // Ancestors: immediate parent first, then grandparent, ..., then root.
        var ancestors = await _unitOfWork.OrgNodes.GetAncestorsAsync(requesterNodeId, ct);
        var ancestorIds = ancestors.Select(a => a.Id).ToHashSet();

        // Level-indexed chain: index 0 (Level 1) = own node, index 1 (Level 2) = immediate parent, etc.
        var levelNodes = new List<OrgNode> { ownNode };
        levelNodes.AddRange(ancestors);

        // Sort definition steps by their declared SortOrder.
        var sortedSteps = definitionSteps.OrderBy(s => s.SortOrder).ToList();

        var plannedSteps = new List<PlannedStepDto>();
        // Dedup across the entire resolved chain: an employee appears only at their earliest level.
        var seenApproverIds = new HashSet<Guid>();

        foreach (var step in sortedSteps)
        {
            if (step.StepType == WorkflowStepType.DirectEmployee)
            {
                // ── DIRECT EMPLOYEE STEP ─────────────────────────────────────────
                if (step.DirectEmployeeId == null)
                    return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingDirectEmployeeId);

                var directEmployee = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, ct);
                if (directEmployee == null)
                    return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.DirectEmployeeNotInCompany);

                // Dedup: if this employee was already added at an earlier step, skip.
                if (seenApproverIds.Contains(directEmployee.Id))
                {
                    _logger.LogInformation("Dedup: DirectEmployee {EmployeeId} already present earlier, skipping", directEmployee.Id);
                    continue;
                }

                seenApproverIds.Add(directEmployee.Id);

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
            }
            else if (step.StepType == WorkflowStepType.HierarchyLevel)
            {
                // ── HIERARCHY LEVEL STEP (dynamic) ───────────────────────────────
                if (!step.LevelsUp.HasValue || step.LevelsUp.Value < 1)
                    return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingLevelsUp);

                var startLevel = step.StartFromLevel ?? 1;
                var endLevel = startLevel + step.LevelsUp.Value - 1;

                for (int level = startLevel; level <= endLevel; level++)
                {
                    // Graceful truncation: requester has fewer ancestors than requested.
                    if (level > levelNodes.Count)
                    {
                        _logger.LogInformation("Requester chain exhausted at level {Level}; stopping HierarchyLevel expansion early", level);
                        break;
                    }

                    var node = levelNodes[level - 1]; // 1-based -> 0-based
                    var managers = await _unitOfWork.OrgNodeAssignments.GetManagersByNodeAsync(node.Id, ct);

                    // D4: skip levels with no managers.
                    if (managers.Count == 0)
                    {
                        _logger.LogInformation("Level {Level} (node {NodeName}) has no managers, skipping", level, node.Name);
                        continue;
                    }

                    // D5: self-approval skip. Exclude the requester from approvers.
                    var approvers = managers
                        .Where(m => m.Id != requesterEmployeeId)
                        .Select(m => new ApproverDto { EmployeeId = m.Id, EmployeeName = m.FullName })
                        .ToList();

                    // Edge: requester was the only manager — the level has no valid approver after self-approval skip.
                    if (approvers.Count == 0)
                    {
                        _logger.LogInformation("Level {Level} skipped: requester was the only manager", level);
                        continue;
                    }

                    // D6: dedup against the chain seen so far.
                    approvers = approvers.Where(a => !seenApproverIds.Contains(a.EmployeeId)).ToList();
                    if (approvers.Count == 0)
                    {
                        _logger.LogInformation("Level {Level} skipped: all managers already present earlier in the chain", level);
                        continue;
                    }

                    foreach (var a in approvers) seenApproverIds.Add(a.EmployeeId);

                    plannedSteps.Add(new PlannedStepDto
                    {
                        StepType = WorkflowStepType.HierarchyLevel,
                        NodeId = node.Id,
                        NodeName = node.Name,
                        SortOrder = 0, // renumbered at the end
                        Approvers = approvers,
                        ResolvedFromLevel = level
                    });
                }
            }
            else
            {
                // ── ORG NODE STEP ────────────────────────────────────────────────
                if (step.OrgNodeId == null)
                    return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingOrgNodeId);

                // Self-approval skip (existing behavior preserved).
                if (step.OrgNodeId == requesterNodeId && isManagerAtOwnNode)
                {
                    _logger.LogInformation("Skipping OrgNode step {SortOrder}: self-approval prevention", step.SortOrder);
                    continue;
                }

                // Hierarchy validation (existing behavior preserved).
                if (!step.BypassHierarchyCheck)
                {
                    if (step.OrgNodeId != requesterNodeId && !ancestorIds.Contains(step.OrgNodeId.Value))
                    {
                        _logger.LogWarning("OrgNode step {SortOrder} references node {NodeId} not in approval path",
                            step.SortOrder, step.OrgNodeId);
                        return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.InvalidWorkflowChain);
                    }
                }

                // Resolve node for its name.
                OrgNode? stepNode;
                if (step.OrgNodeId == requesterNodeId) stepNode = ownNode;
                else if (ancestorIds.Contains(step.OrgNodeId.Value))
                    stepNode = ancestors.First(a => a.Id == step.OrgNodeId.Value);
                else
                {
                    stepNode = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId.Value, ct);
                    if (stepNode == null)
                        return Result.Failure<List<PlannedStepDto>>(DomainErrors.OrgNode.NotFound);
                }

                var managers = await _unitOfWork.OrgNodeAssignments.GetManagersByNodeAsync(step.OrgNodeId.Value, ct);
                if (managers.Count == 0)
                {
                    _logger.LogWarning("No active managers at OrgNode step {SortOrder} (node: {NodeName})",
                        step.SortOrder, stepNode.Name);
                    return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.NoActiveManagersAtStep);
                }

                var approvers = managers
                    .Where(m => m.Id != requesterEmployeeId)
                    .Select(m => new ApproverDto { EmployeeId = m.Id, EmployeeName = m.FullName })
                    .ToList();

                // Edge: requester was the only manager — fall back to including them (existing behavior).
                if (approvers.Count == 0)
                {
                    approvers = managers
                        .Select(m => new ApproverDto { EmployeeId = m.Id, EmployeeName = m.FullName })
                        .ToList();
                }

                // D6: dedup against chain so far.
                approvers = approvers.Where(a => !seenApproverIds.Contains(a.EmployeeId)).ToList();
                if (approvers.Count == 0)
                {
                    _logger.LogInformation("OrgNode step {SortOrder} skipped entirely due to dedup", step.SortOrder);
                    continue;
                }

                foreach (var a in approvers) seenApproverIds.Add(a.EmployeeId);

                plannedSteps.Add(new PlannedStepDto
                {
                    StepType = WorkflowStepType.OrgNode,
                    NodeId = stepNode.Id,
                    NodeName = stepNode.Name,
                    SortOrder = 0, // renumbered at the end
                    Approvers = approvers
                });
            }
        }

        // Renumber SortOrder to 1..N contiguous on the resolved chain.
        for (int i = 0; i < plannedSteps.Count; i++)
            plannedSteps[i].SortOrder = i + 1;

        if (plannedSteps.Count == 0)
        {
            _logger.LogInformation("Approval chain empty for employee {EmployeeId}; request will auto-approve", requesterEmployeeId);
            return Result.Success(plannedSteps);
        }

        _logger.LogInformation("Built approval chain with {StepCount} steps", plannedSteps.Count);
        return Result.Success(plannedSteps);
    }
```

> **Important:** Do not reference `OrgNode.Depth` anywhere — it does not exist on the entity. Levels are derived from position in the `levelNodes` list (index 0 = Level 1 = own node).

**Success criteria:** `dotnet build` succeeds. An existing OrgNode-based definition continues to work unchanged. A HierarchyLevel definition resolves correctly for a test requester.

---

### Phase 7 — Preview endpoint

Add a new query + handler + controller route that calls `BuildApprovalChainAsync` without creating a real `Request`.

#### Step 7.1 — Create the query and handler

**New file:** `HrSystemApp.Application/Features/Requests/Queries/PreviewApprovalChain/PreviewApprovalChainQuery.cs`

```csharp
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.Requests.Queries.PreviewApprovalChain;

public record PreviewApprovalChainQuery : IRequest<Result<List<PlannedStepDto>>>
{
    /// <summary>Optional: preview a saved definition.</summary>
    public Guid? DefinitionId { get; set; }

    /// <summary>Optional: preview an inline draft (for admin UI authoring).</summary>
    public List<WorkflowStepDto>? Steps { get; set; }

    /// <summary>Required: the employee whose chain is being previewed.</summary>
    public Guid RequesterEmployeeId { get; set; }
}

public class PreviewApprovalChainQueryHandler : IRequestHandler<PreviewApprovalChainQuery, Result<List<PlannedStepDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkflowResolutionService _workflowResolutionService;
    private readonly ILogger<PreviewApprovalChainQueryHandler> _logger;

    public PreviewApprovalChainQueryHandler(
        IUnitOfWork unitOfWork,
        IWorkflowResolutionService workflowResolutionService,
        ILogger<PreviewApprovalChainQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _workflowResolutionService = workflowResolutionService;
        _logger = logger;
    }

    public async Task<Result<List<PlannedStepDto>>> Handle(PreviewApprovalChainQuery request, CancellationToken ct)
    {
        // Must supply either DefinitionId or inline Steps (not both, not neither).
        if (request.DefinitionId.HasValue == (request.Steps != null && request.Steps.Count > 0))
        {
            // Both or neither supplied.
            _logger.LogWarning("PreviewApprovalChain: must supply exactly one of DefinitionId or Steps");
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.General.ArgumentError);
        }

        List<WorkflowStepDto> stepsToResolve;
        if (request.DefinitionId.HasValue)
        {
            var definition = await _unitOfWork.RequestDefinitions.GetFirstOrDefaultAsync(
                d => d.Id == request.DefinitionId.Value, ct, d => d.WorkflowSteps);

            if (definition == null)
                return Result.Failure<List<PlannedStepDto>>(DomainErrors.Requests.DefinitionNotFound);

            stepsToResolve = definition.WorkflowSteps.Select(s => new WorkflowStepDto
            {
                StepType = s.StepType,
                OrgNodeId = s.OrgNodeId,
                BypassHierarchyCheck = s.BypassHierarchyCheck,
                DirectEmployeeId = s.DirectEmployeeId,
                StartFromLevel = s.StartFromLevel,
                LevelsUp = s.LevelsUp,
                SortOrder = s.SortOrder
            }).ToList();
        }
        else
        {
            stepsToResolve = request.Steps!;
        }

        // Look up the requester's node assignment (same as CreateRequestCommand).
        var assignment = await _unitOfWork.OrgNodeAssignments.GetByEmployeeWithNodeAsync(request.RequesterEmployeeId, ct);
        if (assignment == null)
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.OrgNode.NotFound);

        return await _workflowResolutionService.BuildApprovalChainAsync(
            request.RequesterEmployeeId,
            assignment.OrgNodeId,
            stepsToResolve,
            ct);
    }
}
```

#### Step 7.2 — Expose the endpoint

**File:** `HrSystemApp.Api/Controllers/RequestDefinitionsController.cs`

**At the top of the file, add the using:**

```csharp
using HrSystemApp.Application.Features.Requests.Queries.PreviewApprovalChain;
```

**Inside the controller class, add this action** (anywhere among the other actions, e.g. after `Get`):

```csharp
    /// <summary>
    /// Preview the resolved approval chain for a specific employee against a definition
    /// (saved or draft). Read-only; does not create a request.
    /// </summary>
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] PreviewApprovalChainQuery query)
    {
        return HandleResult(await _sender.Send(query));
    }
```

**Success criteria:** `POST /api/requestdefinitions/preview` returns a list of `PlannedStepDto` for a valid body like:

```json
{
  "definitionId": "00000000-0000-0000-0000-000000000000",
  "requesterEmployeeId": "00000000-0000-0000-0000-000000000000"
}
```

Authorization uses the controller-level attribute — any authenticated user can call it.

---

### Phase 8 — Unit tests

Tests follow the existing pattern (xUnit + Moq + FluentAssertions). Reference: `tests/HrSystemApp.Tests.Unit/Features/ContactAdmin/GetContactAdminRequestsQueryHandlerTests.cs`.

#### Step 8.1 — Resolution service tests

**New file:** `tests/HrSystemApp.Tests.Unit/Services/WorkflowResolutionServiceHierarchyTests.cs`

Scaffolding (the engineer must flesh out each test body using Moq to set up ancestor lookups, manager lookups, and the `isManagerAtOwnNode` check):

```csharp
using FluentAssertions;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HrSystemApp.Tests.Unit.Services;

public class WorkflowResolutionServiceHierarchyTests
{
    // Helper: stand up a mock unit-of-work with the repositories WorkflowResolutionService calls.
    // Tests should cover at minimum:

    [Fact] public Task HierarchyLevel_Alex_LevelsUp3_BuildsTeamDeptVp() => Task.CompletedTask;            // D1, D8
    [Fact] public Task HierarchyLevel_Sarah_TooFewAncestors_ShortensChainGracefully() => Task.CompletedTask;
    [Fact] public Task HierarchyLevel_RequesterAtRoot_ReturnsOwnNodeOnlyOrEmpty() => Task.CompletedTask;
    [Fact] public Task HierarchyLevel_EmptyManagerLevel_SkippedContinues() => Task.CompletedTask;        // D4
    [Fact] public Task HierarchyLevel_SelfApprovalAtResolvedLevel_Skipped() => Task.CompletedTask;       // D5
    [Fact] public Task HierarchyLevel_DuplicateManagerAcrossLevels_AppearsOnlyAtEarliest() => Task.CompletedTask; // D6
    [Fact] public Task Mixed_DirectEmployee_Then_HierarchyLevel_Expands_Correctly() => Task.CompletedTask;
    [Fact] public Task Mixed_Split_L1to2_Then_DirectEmployee_Then_L3_Renumbered() => Task.CompletedTask;
    [Fact] public Task SortOrders_OnPlannedChain_AreContiguous1ToN() => Task.CompletedTask;
    [Fact] public Task EmptyChain_CallerAutoApproves() => Task.CompletedTask;
}
```

Each `Task.CompletedTask` is a placeholder — fill in the test body by:
1. Creating a fake org tree as `List<OrgNode>`.
2. Mocking `IUnitOfWork.OrgNodes.GetByIdAsync`, `GetAncestorsAsync`.
3. Mocking `IUnitOfWork.OrgNodeAssignments.IsManagerAtNodeAsync`, `GetManagersByNodeAsync`.
4. Calling `service.BuildApprovalChainAsync(...)`.
5. Asserting the resulting chain's length, node IDs, approver IDs, and sort orders.

#### Step 8.2 — Definition validation tests

**New file:** `tests/HrSystemApp.Tests.Unit/Features/Requests/CreateRequestDefinitionValidationTests.cs`

Cover at minimum:
- Overlapping HierarchyLevel ranges → `HierarchyRangesOverlap`.
- `HierarchyLevel` step with `LevelsUp = 0` → `MissingLevelsUp`.
- `HierarchyLevel` step with `OrgNodeId` set → `UnexpectedFieldsOnHierarchyLevelStep`.
- `OrgNode` step with `LevelsUp` set → `HierarchyLevelFieldsOnNonHierarchyStep`.
- Valid definitions for all three use cases pass validation.

**Success criteria:** `dotnet test` — all new tests pass, no existing tests regress.

---

### Phase 9 — Documentation updates

#### Step 9.1 — Update the admin samples file

**File:** `admin_workflow_setup_samples.json`

**Replace the file's entire contents with a valid JSON document containing examples for all three use cases.** Use this template (substitute real-looking UUIDs; these are illustrative):

```json
{
  "useCase1_PureHierarchy": {
    "description": "Dynamic 3-level chain. Works for any requester. Uses levels 1-3 from requester's own node upward.",
    "requestBody": {
      "companyId": "94e16c9e-de85-4bc0-9da5-980bcb013b94",
      "requestType": 0,
      "steps": [
        { "stepType": 2, "startFromLevel": 1, "levelsUp": 3, "sortOrder": 1 }
      ]
    }
  },
  "useCase2_PureDirectEmployee": {
    "description": "A fixed named person approves. No hierarchy involvement.",
    "requestBody": {
      "companyId": "94e16c9e-de85-4bc0-9da5-980bcb013b94",
      "requestType": 1,
      "steps": [
        { "stepType": 1, "directEmployeeId": "00000000-0000-0000-0000-000000000001", "sortOrder": 1 }
      ]
    }
  },
  "useCase3_MixedSplit": {
    "description": "Levels 1-2 from hierarchy, then a fixed special approver, then level 3.",
    "requestBody": {
      "companyId": "94e16c9e-de85-4bc0-9da5-980bcb013b94",
      "requestType": 2,
      "steps": [
        { "stepType": 2, "startFromLevel": 1, "levelsUp": 2, "sortOrder": 1 },
        { "stepType": 1, "directEmployeeId": "00000000-0000-0000-0000-000000000002", "sortOrder": 2 },
        { "stepType": 2, "startFromLevel": 3, "levelsUp": 1, "sortOrder": 3 }
      ]
    }
  },
  "stepTypeReference": {
    "0": "OrgNode (hardcoded org node, managers approve)",
    "1": "DirectEmployee (specific named person approves)",
    "2": "HierarchyLevel (dynamic: resolved from requester's position at submission time)"
  },
  "previewEndpoint": {
    "method": "POST",
    "url": "/api/requestdefinitions/preview",
    "bodyWithSavedDefinition": {
      "definitionId": "<definition-uuid>",
      "requesterEmployeeId": "<employee-uuid>"
    },
    "bodyWithInlineDraft": {
      "steps": [
        { "stepType": 2, "startFromLevel": 1, "levelsUp": 3, "sortOrder": 1 }
      ],
      "requesterEmployeeId": "<employee-uuid>"
    }
  }
}
```

#### Step 9.2 — No other doc updates required

`README.md`, `ORG_NODE_APPROVAL_WORKFLOW_PLAN.md`, etc. do not need changes for this feature.

---

### Phase 10 — Seed data

The goal is to provide at least one worked example of a `HierarchyLevel` definition in a dev/demo environment so the feature is observable end-to-end after migration.

> **Why this is tricky:** A seeded definition must reference a real `CompanyId` and, for the mixed example, a real `DirectEmployeeId`. These differ per environment, so hardcoding GUIDs in the migration won't work on arbitrary databases.

#### Step 10.1 — Idempotent seed via raw SQL in the migration's `Up()`

**File:** the migration generated in Phase 3, Step 3.2.

**At the end of `Up()`** (after `AddColumn` calls), append the following SQL. It inserts a demo definition **only if exactly one company exists** in the `Companies` table and **no existing** `RequestDefinition` already covers `RequestType = 0` (Leave) for that company. This keeps the seeding safe across dev, staging, and prod.

```csharp
            // (Seed) Add one example HierarchyLevel definition for the demo company if conditions are met.
            // Safe: only inserts when exactly one company exists and that company has no existing Leave definition.
            migrationBuilder.Sql(@"
DO $$
DECLARE
    demo_company_id uuid;
    demo_def_id uuid := gen_random_uuid();
    demo_step_id uuid := gen_random_uuid();
BEGIN
    IF (SELECT COUNT(*) FROM ""Companies"") = 1 THEN
        SELECT ""Id"" INTO demo_company_id FROM ""Companies"" LIMIT 1;

        IF NOT EXISTS (
            SELECT 1 FROM ""RequestDefinitions""
            WHERE ""CompanyId"" = demo_company_id AND ""RequestType"" = 0
        ) THEN
            INSERT INTO ""RequestDefinitions"" (""Id"", ""CompanyId"", ""RequestType"", ""IsActive"", ""FormSchemaJson"", ""CreatedAt"", ""UpdatedAt"")
            VALUES (demo_def_id, demo_company_id, 0, TRUE, NULL, NOW(), NOW());

            INSERT INTO ""RequestWorkflowSteps"" (""Id"", ""RequestDefinitionId"", ""StepType"", ""OrgNodeId"", ""BypassHierarchyCheck"", ""DirectEmployeeId"", ""StartFromLevel"", ""LevelsUp"", ""SortOrder"")
            VALUES (demo_step_id, demo_def_id, 2, NULL, FALSE, NULL, 1, 3, 1);
        END IF;
    END IF;
END $$;
");
```

**In `Down()`**, append a matching delete block so the migration is reversible:

```csharp
            migrationBuilder.Sql(@"
DELETE FROM ""RequestWorkflowSteps""
WHERE ""StartFromLevel"" IS NOT NULL OR ""LevelsUp"" IS NOT NULL;
");
```

> **Note:** The `Down` SQL deletes any HierarchyLevel-shaped rows. If production already has manually-created HierarchyLevel definitions by the time Down runs, this will remove them too — which is correct, since the columns themselves are being dropped.

#### Step 10.2 — Verify column names match

Before finalizing, open the `ApplicationDbContextModelSnapshot.cs` under `HrSystemApp.Infrastructure/Migrations/` to confirm column names match the SQL above. In particular:
- Double-check whether the snapshot uses `""CreatedAt""` / `""UpdatedAt""` vs other names (it inherits from `AuditableEntity`).
- Check if `BaseEntity`/`AuditableEntity` has any additional required columns (`CreatedBy`, `UpdatedBy`, `IsDeleted`, etc.) that need values in the INSERTs.

If the audited columns require values, extend the `INSERT` lists accordingly (usual defaults: `CreatedBy`/`UpdatedBy` = NULL, `IsDeleted` = FALSE).

**Success criteria:** On a fresh dev database with exactly one company, the migration inserts one Leave definition whose single step is a `HierarchyLevel` with levels 1–3. Submitting a Leave request as any employee resolves dynamically to their own team lead → dept manager → VP (or fewer, depending on their position).

---

## 4. Final verification checklist

Run this full checklist before merging.

1. **Build passes.** `dotnet build` with no errors or warnings from changed files.
2. **All unit tests pass.** `dotnet test`. New tests from Phase 8 pass; no existing test regresses.
3. **Migration applies cleanly.** On a fresh DB: `dotnet ef database update` succeeds. On a DB with existing definitions: same, and existing rows have NULL `StartFromLevel` / `LevelsUp`.
4. **Migration is reversible.** `dotnet ef database update <PreviousMigrationName>` rolls back cleanly.
5. **Existing OrgNode definitions still work.** Submit a Leave request against an existing OrgNode-only definition; chain resolves exactly as before.
6. **HierarchyLevel pure case works end-to-end.** Create a definition `[{ HierarchyLevel, levelsUp=3 }]`. Submit a request as employee Alex (with a 3+ level chain). Verify `PlannedStepsJson` shows three steps: own node → parent → grandparent, each with correct `ResolvedFromLevel`.
7. **HierarchyLevel truncation works.** Submit the same definition as an employee with fewer than 3 ancestors. Chain is shortened — not failed.
8. **Mixed split chain works.** Create `[{ HL levels 1-2 }, { DirectEmployee X }, { HL level 3 }]`. Submit as Alex. Verify planned chain: Team A mgr → Dep1 mgr → X → VP1 mgr, with contiguous SortOrder 1..4.
9. **Overlapping ranges rejected at create time.** POST a definition with `[{ HL 1-2 }, { HL 2-3 }]`. Expect `Request.HierarchyRangesOverlap`.
10. **Empty-manager level skipped.** Seed a node (e.g., Dep2) with zero managers. Submit as an employee under Dep2 with `HL levels 1-3`. Chain includes Team + VP only.
11. **Self-approval skipped at resolved level.** If the requester is a manager at Team A, HierarchyLevel step for level 1 drops them from approvers (or skips the level if they were the only manager and no one else).
12. **Dedup works.** Set up a case where Maya is a manager at both Dep1 and VP1. Submit as Alex with `HL 1-3`. Maya appears exactly once (at the earlier level). VP1 level either uses different managers or is skipped if Maya was its only manager.
13. **Preview endpoint matches submission.** For the same definition + requester, `POST /api/requestdefinitions/preview` returns the same `plannedSteps` shape that `POST /api/requests` would snapshot into `PlannedStepsJson`.
14. **Preview endpoint auth.** Any authenticated employee can call it; unauthenticated calls return 401.
15. **Seed visible after migration.** On dev DB with one company, after migration, `GET /api/requestdefinitions` shows one new Leave definition with a single HierarchyLevel step.

---

## 5. Files changed / added — summary table

| Phase | File | Change |
|---|---|---|
| 1 | `HrSystemApp.Domain/Enums/WorkflowStepType.cs` | Add `HierarchyLevel = 2` |
| 1 | `HrSystemApp.Domain/Models/RequestWorkflow.cs` | Add `StartFromLevel`, `LevelsUp` on `RequestWorkflowStep` |
| 2 | `HrSystemApp.Application/DTOs/Requests/PlannedStepDto.cs` | Extend `WorkflowStepDto` + `PlannedStepDto` |
| 2 | `HrSystemApp.Application/Features/Requests/Queries/GetRequestDefinitions/GetRequestDefinitionsQuery.cs` | Map new fields in response |
| 3 | `HrSystemApp.Infrastructure/Data/Configurations/RequestRelatedConfigurations.cs` | Register new EF properties |
| 3 | `HrSystemApp.Infrastructure/Migrations/{ts}_AddHierarchyLevelWorkflowStep.cs` | NEW — column additions + seed SQL |
| 4 | `HrSystemApp.Application/Errors/DomainErrors.cs` | Add 5 new errors under `Request` |
| 5 | `HrSystemApp.Application/Features/Requests/Commands/Admin/CreateRequestDefinitionCommand.cs` | Add validation blocks A + B, update entity projection |
| 5 | `HrSystemApp.Application/Features/Requests/Commands/Admin/UpdateRequestDefinitionCommand.cs` | Same as above |
| 6 | `HrSystemApp.Infrastructure/Services/WorkflowResolutionService.cs` | Rewrite `BuildApprovalChainAsync` |
| 7 | `HrSystemApp.Application/Features/Requests/Queries/PreviewApprovalChain/PreviewApprovalChainQuery.cs` | NEW — query + handler |
| 7 | `HrSystemApp.Api/Controllers/RequestDefinitionsController.cs` | Add `POST /preview` action |
| 8 | `tests/HrSystemApp.Tests.Unit/Services/WorkflowResolutionServiceHierarchyTests.cs` | NEW — resolution tests |
| 8 | `tests/HrSystemApp.Tests.Unit/Features/Requests/CreateRequestDefinitionValidationTests.cs` | NEW — validation tests |
| 9 | `admin_workflow_setup_samples.json` | Rewrite with new format examples |

**No changes required** to `CreateRequestCommand.cs` — the existing submission flow is transparent to the expansion. Its call to `BuildApprovalChainAsync` + serialization of `PlannedStepsJson` continue to work.

---

## 6. Glossary (quick reference)

- **Level 1** = the requester's own node. **Level 2** = immediate parent. **Level N** = (N−1)th ancestor.
- **HierarchyLevel step** = `{ StepType = 2, StartFromLevel = a, LevelsUp = b }` covering levels `[a, a + b − 1]`.
- **Snapshot** = `Request.PlannedStepsJson`; the resolved chain frozen at submission.
- **Dedup** = removing the same `EmployeeId` from later planned steps when they already appeared at an earlier one.
- **Self-approval skip** = excluding the requester from approver list at any level where they're a manager; dropping the level if they were the only manager.

---

*Plan written to be implementable phase-by-phase with minimal ambiguity. If any step feels underspecified, re-read the Key Design Decisions (D1–D9) table in section 1 — they are the source of truth.*
