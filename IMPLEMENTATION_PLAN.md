# Implementation Plan: Mixed Workflow Step Types

## Overview

This plan covers two related features for the request definition approval workflow system:

1. **Direct Employee Step** — When building an approval chain for a request definition, an admin can now insert a specific named employee as an approver at any position in the chain. This employee does not need to exist anywhere in the org node hierarchy. They are just a company employee.

2. **BypassHierarchyCheck on OrgNode Step** — An existing OrgNode step can be flagged to bypass the ancestor validation. This is needed when you want a specific department (e.g. an HR node) to approve requests even though that node is not an ancestor of the requester's node in the org tree.

The current system only supports OrgNode-based steps where every step must reference a node that is either the requester's own node or one of its ancestors. These two features break that restriction in controlled ways.

---

## Background: How the System Currently Works

Understanding this is essential before touching anything.

### Request Definition

A `RequestDefinition` belongs to a company and defines the approval chain for a specific request type (Leave, Loan, etc.). It contains a list of `RequestWorkflowStep` entities, each referencing an `OrgNode` and a `SortOrder`.

### Request Submission Flow

When an employee submits a request:
1. The system loads the `RequestDefinition` for that company + request type.
2. It calls `WorkflowResolutionService.BuildApprovalChainAsync(...)` which:
   - Looks up the requester's org node assignment.
   - Gets all ancestors of that node.
   - For each step in the definition, resolves the managers at that step's OrgNode.
   - Validates each step's OrgNode is either the requester's own node or an ancestor.
   - Excludes the requester from approvers (self-approval prevention).
3. The result is a `List<PlannedStepDto>` which is serialized into `Request.PlannedStepsJson` — a snapshot of who must approve at each step.
4. `Request.CurrentStepApproverIds` is a denormalized comma-separated list of approver employee IDs for the current step, used for fast DB filtering.

### Approval Flow

When an approver acts on a request, the system deserializes `PlannedStepsJson`, finds the current step by `CurrentStepOrder`, checks the acting employee is in that step's `Approvers` list, records the history, and advances `CurrentStepOrder`. When all steps are done, the request is marked Approved.

---

## What Has Already Been Done

Do NOT redo any of these. They are already in the codebase.

### 1. New Enum: `WorkflowStepType`
**File:** `HrSystemApp.Domain/Enums/WorkflowStepType.cs`

Already created with:
```csharp
public enum WorkflowStepType
{
    OrgNode = 0,
    DirectEmployee = 1
}
```

### 2. Updated Entity: `RequestWorkflowStep`
**File:** `HrSystemApp.Domain/Models/RequestWorkflow.cs`

The entity already has these new fields:
- `WorkflowStepType StepType` — defaults to `OrgNode`
- `Guid? OrgNodeId` — now nullable (was previously non-nullable)
- `bool BypassHierarchyCheck` — defaults to false
- `Guid? DirectEmployeeId` — nullable, only set when StepType = DirectEmployee
- `OrgNode? OrgNode` — navigation, now nullable
- `Employee? DirectEmployee` — new navigation property

### 3. Updated EF Configuration
**File:** `HrSystemApp.Infrastructure/Data/Configurations/RequestRelatedConfigurations.cs`

Already configured:
- `OrgNodeId` FK is now optional, `OnDelete = Restrict`
- `DirectEmployeeId` FK points to `Employees`, optional, `OnDelete = Restrict`
- `StepType` is required
- `BypassHierarchyCheck` has default value false

### 4. Migration Written
**File:** `HrSystemApp.Infrastructure/Migrations/20260418000000_AddMixedWorkflowStepTypes.cs`

**IMPORTANT — This migration has a bug that must be fixed before running it.** The migration currently adds a `Category` column to `RequestDefinitions`. This must be removed. The Category feature was scrapped. See the correction instructions in the "Fixes Required" section below.

### 5. Model Snapshot Updated
**File:** `HrSystemApp.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`

Similarly has a `Category` property added to `RequestDefinition` that must be removed.

### 6. New Domain Errors Added
**File:** `HrSystemApp.Application/Errors/DomainErrors.cs`

The following errors were added inside `DomainErrors.Request`:
```csharp
OrgNodeNotInCompany
DirectEmployeeNotInCompany
DirectEmployeeAlsoNodeManager
DirectEmployeeNotActive
MissingOrgNodeId
MissingDirectEmployeeId
```

### 7. Updated DTOs
**File:** `HrSystemApp.Application/DTOs/Requests/PlannedStepDto.cs`

`WorkflowStepDto` now has:
- `WorkflowStepType StepType`
- `Guid? OrgNodeId`
- `bool BypassHierarchyCheck`
- `Guid? DirectEmployeeId`
- `int SortOrder`

`PlannedStepDto` now has:
- `WorkflowStepType StepType`
- `Guid? NodeId` (was `Guid NodeId`)
- `string NodeName`
- `int SortOrder`
- `List<ApproverDto> Approvers`

---

## Fixes Required Before Implementing New Code

### Fix 1: Remove Category from Migration

**File:** `HrSystemApp.Infrastructure/Migrations/20260418000000_AddMixedWorkflowStepTypes.cs`

In the `Up()` method, delete this entire block:
```csharp
// 1. Add Category to RequestDefinitions
migrationBuilder.AddColumn<string>(
    name: "Category",
    table: "RequestDefinitions",
    type: "character varying(100)",
    maxLength: 100,
    nullable: true);
```

In the `Down()` method, delete this entire block:
```csharp
migrationBuilder.DropColumn(
    name: "Category",
    table: "RequestDefinitions");
```

Also renumber the remaining comments (step 1 becomes "Add StepType", etc.) for clarity.

### Fix 2: Remove Category from Model Snapshot

**File:** `HrSystemApp.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`

Find the `RequestDefinition` entity block in `BuildTargetModel`. Remove this property:
```csharp
b.Property<string>("Category")
    .HasMaxLength(100)
    .HasColumnType("character varying(100)");
```

---

## What Still Needs to Be Implemented

---

### Task 1: Update `WorkflowResolutionService`

**File:** `HrSystemApp.Infrastructure/Services/WorkflowResolutionService.cs`

This is the most important file to change. It currently only handles OrgNode steps. It needs to branch on `StepType`.

#### Method: `BuildApprovalChainAsync`

The method signature stays exactly the same. Only the body changes.

The existing logic around getting `isManagerAtOwnNode`, `ownNode`, and `ancestors` at the top of the method should remain. It is still needed for OrgNode steps.

Replace the `foreach` loop body with the following branching logic:

```
foreach (var step in sortedSteps)
{
    if (step.StepType == WorkflowStepType.DirectEmployee)
    {
        // ── DIRECT EMPLOYEE STEP ─────────────────────────────────────────────

        if (step.DirectEmployeeId == null)
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingDirectEmployeeId);

        // Load the employee. The repository method GetByIdAsync already exists.
        var directEmployee = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, ct);
        if (directEmployee == null)
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.DirectEmployeeNotInCompany);

        // No hierarchy validation. No self-approval skip.
        // The admin explicitly named this person. They approve regardless.

        plannedSteps.Add(new PlannedStepDto
        {
            StepType = WorkflowStepType.DirectEmployee,
            NodeId = null,
            NodeName = directEmployee.FullName,
            SortOrder = step.SortOrder,
            Approvers = new List<ApproverDto>
            {
                new ApproverDto
                {
                    EmployeeId = directEmployee.Id,
                    EmployeeName = directEmployee.FullName
                }
            }
        });
    }
    else
    {
        // ── ORG NODE STEP ────────────────────────────────────────────────────

        if (step.OrgNodeId == null)
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingOrgNodeId);

        // Self-approval skip: if this node is the requester's own node
        // and the requester is a manager there, skip this step.
        if (step.OrgNodeId == requesterNodeId && isManagerAtOwnNode)
        {
            _logger.LogInformation("Skipping step {SortOrder}: employee {EmployeeId} is a manager at node {NodeId} (self-approval prevention)",
                step.SortOrder, requesterEmployeeId, requesterNodeId);
            continue;
        }

        // Hierarchy validation (skip if BypassHierarchyCheck is true)
        if (!step.BypassHierarchyCheck)
        {
            if (step.OrgNodeId != requesterNodeId && !ancestorIds.Contains(step.OrgNodeId.Value))
            {
                _logger.LogWarning("Workflow step {StepOrder} references node {NodeId} which is not in the approval path",
                    step.SortOrder, step.OrgNodeId);
                return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.InvalidWorkflowChain);
            }
        }

        // Resolve the node object for its name
        OrgNode stepNode;
        if (step.OrgNodeId == requesterNodeId)
        {
            stepNode = ownNode;
        }
        else if (ancestorIds.Contains(step.OrgNodeId.Value))
        {
            stepNode = ancestors.First(a => a.Id == step.OrgNodeId.Value);
        }
        else
        {
            // BypassHierarchyCheck is true and it's not an ancestor — load it fresh
            stepNode = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId.Value, ct);
            if (stepNode == null)
                return Result.Failure<List<PlannedStepDto>>(DomainErrors.OrgNode.NotFound);
        }

        // Get managers at this node
        var managers = await _unitOfWork.OrgNodeAssignments.GetManagersByNodeAsync(step.OrgNodeId.Value, ct);
        if (managers.Count == 0)
        {
            _logger.LogWarning("No active managers at step {StepOrder} (node: {NodeName})",
                step.SortOrder, stepNode.Name);
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.NoActiveManagersAtStep);
        }

        // Exclude requester from approvers (self-approval prevention)
        var approvers = managers
            .Where(m => m.Id != requesterEmployeeId)
            .Select(m => new ApproverDto { EmployeeId = m.Id, EmployeeName = m.FullName })
            .ToList();

        // Edge case: requester was the only manager — keep them
        if (approvers.Count == 0)
        {
            approvers = managers
                .Select(m => new ApproverDto { EmployeeId = m.Id, EmployeeName = m.FullName })
                .ToList();
            _logger.LogInformation("Self-approval edge case: requester is the only manager at step {SortOrder}", step.SortOrder);
        }

        plannedSteps.Add(new PlannedStepDto
        {
            StepType = WorkflowStepType.OrgNode,
            NodeId = stepNode.Id,
            NodeName = stepNode.Name,
            SortOrder = step.SortOrder,
            Approvers = approvers
        });
    }
}
```

The rest of the method (the empty chain check + auto-approval log at the bottom) stays exactly the same.

#### Method: `ValidateWorkflowStepsAsync`

Apply the same branching. For DirectEmployee steps, just check the employee exists. For OrgNode steps, apply the existing logic but with the `BypassHierarchyCheck` flag respected.

Replace the `foreach` loop body:

```
foreach (var step in definitionSteps)
{
    if (step.StepType == WorkflowStepType.DirectEmployee)
    {
        if (step.DirectEmployeeId == null)
            return Result.Failure(DomainErrors.Request.MissingDirectEmployeeId);

        var emp = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, ct);
        if (emp == null)
            return Result.Failure(DomainErrors.Request.DirectEmployeeNotInCompany);

        // No hierarchy validation needed for direct employee steps
        continue;
    }

    // OrgNode step
    if (step.OrgNodeId == null)
        return Result.Failure(DomainErrors.Request.MissingOrgNodeId);

    if (step.OrgNodeId == requesterNodeId && isManagerAtOwnNode)
        continue; // will be skipped at build time

    if (!step.BypassHierarchyCheck)
    {
        if (step.OrgNodeId != requesterNodeId && !ancestorIds.Contains(step.OrgNodeId.Value))
            return Result.Failure(DomainErrors.Request.InvalidWorkflowChain);
    }

    var managers = await _unitOfWork.OrgNodeAssignments.GetManagersByNodeAsync(step.OrgNodeId.Value, ct);
    if (managers.Count == 0)
        return Result.Failure(DomainErrors.Request.NoActiveManagersAtStep);
}
```

---

### Task 2: Update `IWorkflowResolutionService` XML Comments

**File:** `HrSystemApp.Application/Interfaces/Services/IWorkflowResolutionService.cs`

No signature changes. Just update the XML doc summary on `BuildApprovalChainAsync` to mention:
- OrgNode steps validate ancestor chain (unless BypassHierarchyCheck is true)
- DirectEmployee steps resolve to one named employee with no hierarchy validation

---

### Task 3: Update `CreateRequestDefinitionCommand`

**File:** `HrSystemApp.Application/Features/Requests/Commands/Admin/CreateRequestDefinitionCommand.cs`

#### What changes in the command record

No new fields needed. The `Steps` property already uses `List<WorkflowStepDto>` which now carries `StepType`, `OrgNodeId`, `BypassHierarchyCheck`, and `DirectEmployeeId`.

#### What changes in the handler

Replace the current step validation block (which only checks OrgNode exists) and the definition construction block with the logic below.

**Step validation — replace the existing OrgNode-only loop:**

```
// 2. Validate steps have unique sort orders (keep existing code — no change)

// 3. Validate each step's referenced entity exists and belongs to this company
foreach (var step in request.Steps)
{
    if (step.StepType == WorkflowStepType.OrgNode)
    {
        if (!step.OrgNodeId.HasValue)
            return Result.Failure<Guid>(DomainErrors.Request.MissingOrgNodeId);

        var node = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId.Value, cancellationToken);
        if (node == null)
            return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);

        if (node.CompanyId != targetCompanyId)
            return Result.Failure<Guid>(DomainErrors.Request.OrgNodeNotInCompany);
    }
    else if (step.StepType == WorkflowStepType.DirectEmployee)
    {
        if (!step.DirectEmployeeId.HasValue)
            return Result.Failure<Guid>(DomainErrors.Request.MissingDirectEmployeeId);

        var emp = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, cancellationToken);
        if (emp == null)
            return Result.Failure<Guid>(DomainErrors.Employee.NotFound);

        if (emp.CompanyId != targetCompanyId)
            return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeNotInCompany);
    }
}
```

**Cross-step conflict check — add this block after the per-step loop:**

This prevents an employee named as a DirectEmployee step from also being a manager at any OrgNode step in the same chain. The rule is: if you are explicitly named as a personal approver, you cannot also be one of the OrgNode managers in the same definition.

```
// 4. Cross-step conflict check:
//    A DirectEmployee approver must not also be a manager at any OrgNode step.
var directEmployeeIds = request.Steps
    .Where(s => s.StepType == WorkflowStepType.DirectEmployee && s.DirectEmployeeId.HasValue)
    .Select(s => s.DirectEmployeeId!.Value)
    .ToHashSet();

if (directEmployeeIds.Count > 0)
{
    var orgNodeSteps = request.Steps.Where(s => s.StepType == WorkflowStepType.OrgNode && s.OrgNodeId.HasValue);
    foreach (var nodeStep in orgNodeSteps)
    {
        var managers = await _unitOfWork.OrgNodeAssignments.GetManagersByNodeAsync(nodeStep.OrgNodeId!.Value, cancellationToken);
        var conflict = managers.Any(m => directEmployeeIds.Contains(m.Id));
        if (conflict)
            return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeAlsoNodeManager);
    }
}
```

**Entity construction — replace the existing Steps mapping:**

```csharp
var definition = new RequestDefinition
{
    CompanyId = targetCompanyId,
    RequestType = request.RequestType,
    IsActive = true,
    WorkflowSteps = request.Steps.Select(s => new RequestWorkflowStep
    {
        StepType          = s.StepType,
        OrgNodeId         = s.StepType == WorkflowStepType.OrgNode ? s.OrgNodeId : null,
        BypassHierarchyCheck = s.StepType == WorkflowStepType.OrgNode && s.BypassHierarchyCheck,
        DirectEmployeeId  = s.StepType == WorkflowStepType.DirectEmployee ? s.DirectEmployeeId : null,
        SortOrder         = s.SortOrder
    }).ToList()
};
```

---

### Task 4: Update `UpdateRequestDefinitionCommand`

**File:** `HrSystemApp.Application/Features/Requests/Commands/Admin/UpdateRequestDefinitionCommand.cs`

Apply the exact same changes as Task 3 to this handler. The `targetCompanyId` here is `definition.CompanyId` (already loaded from DB).

Replace the existing step validation loop and the `WorkflowSteps` assignment with the same logic shown in Task 3.

The per-step validation loop, cross-step conflict check, and entity mapping are identical — only `targetCompanyId` comes from a different source.

---

### Task 5: Update `CreateRequestCommand`

**File:** `HrSystemApp.Application/Features/Requests/Commands/CreateRequest/CreateRequestCommand.cs`

Two changes needed here.

#### Change 1: Map all new fields when building `definitionSteps`

Find this existing code (step 5):
```csharp
var definitionSteps = definition.WorkflowSteps
    .Select(s => new WorkflowStepDto { OrgNodeId = s.OrgNodeId, SortOrder = s.SortOrder })
    .ToList();
```

Replace it with:
```csharp
var definitionSteps = definition.WorkflowSteps
    .Select(s => new WorkflowStepDto
    {
        StepType             = s.StepType,
        OrgNodeId            = s.OrgNodeId,
        BypassHierarchyCheck = s.BypassHierarchyCheck,
        DirectEmployeeId     = s.DirectEmployeeId,
        SortOrder            = s.SortOrder
    })
    .ToList();
```

Without this, all steps would get `StepType = OrgNode` by default and DirectEmployee steps would be ignored.

#### Change 2: Add submission-time re-validation

Add this block after building `definitionSteps` and before calling `BuildApprovalChainAsync`. This re-validates that referenced entities are still valid at the moment of submission (the definition might have been created weeks ago and things could have changed):

```
// 5b. Submission-time validation: ensure all referenced entities still exist and belong to this company
foreach (var step in definitionSteps)
{
    if (step.StepType == WorkflowStepType.OrgNode && step.OrgNodeId.HasValue)
    {
        var node = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId.Value, cancellationToken);
        if (node == null || node.CompanyId != employee.CompanyId)
            return Result.Failure<Guid>(DomainErrors.Request.OrgNodeNotInCompany);
    }
    else if (step.StepType == WorkflowStepType.DirectEmployee && step.DirectEmployeeId.HasValue)
    {
        var directEmp = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, cancellationToken);
        if (directEmp == null || directEmp.CompanyId != employee.CompanyId)
            return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeNotInCompany);

        // Also check employee is still active
        if (directEmp.EmploymentStatus != EmploymentStatus.Active)
            return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeNotActive);
    }
}
```

Everything else in this handler (the `BuildApprovalChainAsync` call, auto-approval logic, persisting) stays exactly as-is.

---

### Task 6: Update `GetRequestDefinitionsQuery`

**File:** `HrSystemApp.Application/Features/Requests/Queries/GetRequestDefinitions/GetRequestDefinitionsQuery.cs`

The `Steps` mapping on line 70 currently only maps `OrgNodeId` and `SortOrder`:
```csharp
Steps = d.WorkflowSteps.Select(s => new WorkflowStepDto { OrgNodeId = s.OrgNodeId, SortOrder = s.SortOrder }).ToList(),
```

Replace it with:
```csharp
Steps = d.WorkflowSteps.Select(s => new WorkflowStepDto
{
    StepType             = s.StepType,
    OrgNodeId            = s.OrgNodeId,
    BypassHierarchyCheck = s.BypassHierarchyCheck,
    DirectEmployeeId     = s.DirectEmployeeId,
    SortOrder            = s.SortOrder
}).ToList(),
```

This ensures when the frontend fetches definitions, it can see the full step configuration and render it correctly.

---

### Task 7: Verify `ApproveRequestCommand` — No Changes Needed

**File:** `HrSystemApp.Application/Features/Requests/Commands/ApproveRequest/ApproveRequestCommand.cs`

This file deserializes `PlannedStepsJson` into `List<PlannedStepDto>` and checks `currentStep.Approvers`. Since:
- Both OrgNode and DirectEmployee steps produce a populated `Approvers` list
- The approval check is just `approverIds.Contains(employee.Id)` — which works for both step types

No changes are needed here. Just verify it compiles cleanly after the DTO changes.

---

## Repository Methods Required

The following repository methods are called in the new code. Verify they already exist before running a build:

| Method | Repository | Notes |
|--------|-----------|-------|
| `GetByIdAsync(Guid id, CancellationToken ct)` | `IEmployeeRepository` | Used to load a direct employee by ID |
| `GetByIdAsync(Guid id, CancellationToken ct)` | `IOrgNodeRepository` | Already used throughout — verify it exists |
| `GetManagersByNodeAsync(Guid nodeId, CancellationToken ct)` | `IOrgNodeAssignmentRepository` | Already used in WorkflowResolutionService |

If `IEmployeeRepository.GetByIdAsync` does not exist, add it to the interface and implement it in the concrete repository using the standard pattern already used for other entities in the project.

---

## Validation Rules Summary

This section is a condensed reference for all new validation rules and where they are enforced.

### At Definition Create / Update

| Rule | Error Returned |
|------|---------------|
| OrgNode step has no OrgNodeId | `Request.MissingOrgNodeId` |
| OrgNode does not exist in DB | `OrgNode.NotFound` |
| OrgNode belongs to a different company | `Request.OrgNodeNotInCompany` |
| DirectEmployee step has no DirectEmployeeId | `Request.MissingDirectEmployeeId` |
| DirectEmployee does not exist in DB | `Employee.NotFound` |
| DirectEmployee belongs to a different company | `Request.DirectEmployeeNotInCompany` |
| DirectEmployee is also a manager at any OrgNode in the same chain | `Request.DirectEmployeeAlsoNodeManager` |
| Two steps have the same SortOrder | `General.ArgumentError` (existing) |

### At Request Submission

| Rule | Error Returned |
|------|---------------|
| OrgNode step's node no longer exists or belongs to a different company | `Request.OrgNodeNotInCompany` |
| DirectEmployee no longer exists or belongs to a different company | `Request.DirectEmployeeNotInCompany` |
| DirectEmployee's EmploymentStatus is not Active | `Request.DirectEmployeeNotActive` |

### At Workflow Resolution (BuildApprovalChainAsync)

| Rule | Error Returned |
|------|---------------|
| OrgNode step has no OrgNodeId | `Request.MissingOrgNodeId` |
| OrgNode step references a node not in ancestor chain (and BypassHierarchyCheck = false) | `Request.InvalidWorkflowChain` |
| OrgNode step has no managers assigned | `Request.NoActiveManagersAtStep` |
| DirectEmployee step has no DirectEmployeeId | `Request.MissingDirectEmployeeId` |
| DirectEmployee not found | `Request.DirectEmployeeNotInCompany` |

---

## Key Behavioral Rules to Preserve

- **Self-approval prevention for OrgNode steps** — If the requester is a manager at their own node and a step references that node, the step is skipped. If they are the only manager, they are kept (forced self-approval edge case). This logic is **only for OrgNode steps**.
- **No self-approval logic for DirectEmployee steps** — The admin explicitly named this person. If the requester happens to be that person, they still appear in the approver list for that step. There is no skip.
- **BypassHierarchyCheck only applies to OrgNode steps** — It has no meaning on a DirectEmployee step and should be ignored/stored as false for those steps.
- **PlannedStepsJson is a snapshot** — Once a request is submitted, its approval chain is frozen in `PlannedStepsJson`. Changes to the definition after submission do not affect in-flight requests.
- **CurrentStepApproverIds is denormalized** — It is a comma-separated string of employee IDs for the current step's approvers. It works the same for both step types since both produce an `Approvers` list.

---

## Build Order

Implement in this order to avoid compilation errors at each step:

1. Fix migration (remove Category — Task 0)
2. Fix model snapshot (remove Category — Task 0)
3. Update `WorkflowResolutionService` (Task 1) — depends on the already-updated DTOs
4. Update `IWorkflowResolutionService` comments (Task 2)
5. Update `CreateRequestDefinitionCommand` (Task 3)
6. Update `UpdateRequestDefinitionCommand` (Task 4)
7. Update `CreateRequestCommand` (Task 5)
8. Update `GetRequestDefinitionsQuery` (Task 6)
9. Build the full solution and fix any compilation errors
10. Verify `ApproveRequestCommand` compiles cleanly (Task 7)
