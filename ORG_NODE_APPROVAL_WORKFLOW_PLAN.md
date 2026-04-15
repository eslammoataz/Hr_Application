# Plan: OrgNode-Based Approval Workflow

## Context
Refactor the Request Approval Workflow to use a strict OrgNode hierarchy-based model instead of role-based approval.

**Key Changes:**
- `WorkflowStep.RequiredRole (UserRole)` → `WorkflowStep.OrgNodeId (Guid)` — step references an OrgNode in the hierarchy
- `Request.CurrentApproverId (Guid?)` → `Request.CurrentStepOrder (int)` — tracks current step by order
- `Request.PlannedChainJson` → `Request.PlannedStepsJson` — new structure: `[{nodeId, nodeName, approvers: [{id, name}]}]`
- Approval chain = ancestor OrgNodes of the requester's node
- Approvers at each step = employees with `OrgRole = Manager` at that step's OrgNode
- Self-approval: exclude requester from approvers at creation time
- If no active managers at a step → throw error at request creation (not approval time)
- Rejection immediately sets status to Rejected
- Remove all role-based `ResolveApproverAsync`, `FindFirstEmployeeInRoleAsync` logic

---

## Phase A: Domain Model Changes

### 1. RequestWorkflow.cs — WorkflowStep
- Change `RequiredRole (UserRole)` → `OrgNodeId (Guid)`
- No navigation to UserRole

### 2. Request.cs
- Remove `CurrentApproverId (Guid?)`
- Add `CurrentStepOrder (int)` — starts at 1, when exceeded request is Approved
- Rename `PlannedChainJson` → `PlannedStepsJson`
- Remove `CurrentApprover` navigation

### 3. DomainErrors.cs
- Add `Request.NoActiveManagersAtStep(nodeId, stepOrder)` error
- Add `Request.InvalidWorkflowChain()` error

---

## Phase B: New Repository Methods

### IOrgNodeRepository.cs — add methods:
```csharp
Task<IReadOnlyList<OrgNode>> GetAncestorsAsync(Guid nodeId, CancellationToken ct);
// Returns all ancestors from immediate parent to root (ordered by depth ascending)

Task<IReadOnlyList<Employee>> GetManagersOfNodeAsync(Guid nodeId, CancellationToken ct);
// Returns employees assigned to node with OrgRole = Manager (excludes IsDeleted)

Task<IReadOnlyList<OrgNode>> GetAncestorChainAsync(Guid startNodeId, Guid targetRootId, CancellationToken ct);
// Returns ancestor chain from startNode up to (but not including) targetRootId
```

### OrgNodeRepository.cs — implement:
- `GetAncestorsAsync` — walk up ParentId recursively, include Parent at each level
- `GetManagersOfNodeAsync` — use OrgNodeAssignments with OrgRole=Manager, Include Employee, exclude IsDeleted

### IOrgNodeAssignmentRepository.cs — add method:
```csharp
Task<IReadOnlyList<Employee>> GetManagersByNodeAsync(Guid nodeId, CancellationToken ct);
// Returns managers at a specific node
```

### OrgNodeAssignmentRepository.cs — implement:
- Uses `Where(a => a.OrgNodeId == nodeId && a.Role == OrgRole.Manager && !a.IsDeleted).Include(a => a.Employee).Select(a => a.Employee)`

---

## Phase C: New DTOs

### DTOs/Requests/PlannedStepDto.cs
```csharp
public class PlannedStepDto
{
    public Guid NodeId { get; set; }
    public string NodeName { get; set; }
    public int SortOrder { get; set; }
    public List<ApproverDto> Approvers { get; set; } = new();
}

public class ApproverDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
}
```

---

## Phase D: New Service — IWorkflowResolutionService

### IWorkflowResolutionService.cs (Application/Interfaces/Services/)
```csharp
public interface IWorkflowResolutionService
{
    Task<Result<List<PlannedStepDto>>> BuildApprovalChainAsync(
        Guid requesterEmployeeId,
        Guid requesterNodeId,
        List<WorkflowStepDto> definitionSteps,
        CancellationToken ct);

    Task<Result> ValidateWorkflowStepsAsync(
        Guid requesterNodeId,
        List<WorkflowStepDto> definitionSteps,
        CancellationToken ct);
}

public class WorkflowStepDto
{
    public Guid OrgNodeId { get; set; }
    public int SortOrder { get; set; }
}
```

### WorkflowResolutionService.cs (Infrastructure/Services/)
- `BuildApprovalChainAsync`:
  1. Get all ancestors of requester's node via `GetAncestorsAsync`
  2. For each definition step, verify OrgNodeId is in the ancestor chain
  3. Throw `InvalidWorkflowChain` if any step references a non-ancestor node
  4. For each step, get managers via `GetManagersByNodeAsync`
  5. If no managers at any step → throw `NoActiveManagersAtStep`
  6. Exclude requester from approvers at each step
  7. Return `List<PlannedStepDto>` sorted by SortOrder

- `ValidateWorkflowStepsAsync`:
  1. Get ancestors of requester node
  2. Check all definition steps reference valid ancestors
  3. Check all steps have at least one active manager

---

## Phase E: Update CreateRequestDefinition

### CreateRequestDefinitionCommand.cs
- `WorkflowStepDto`: `{ Guid OrgNodeId, int SortOrder }` (remove UserRole RequiredRole)

### CreateRequestDefinitionCommandValidator.cs
- Validate OrgNodeId is not empty
- Validate SortOrder > 0
- Validate no duplicate SortOrder values

---

## Phase F: Update CreateRequest Command

### CreateRequestCommand.cs
- No structural changes, uses existing properties

### CreateRequestCommandHandler.cs
1. Validate employee exists and has a node assignment
2. Call `WorkflowResolutionService.ValidateWorkflowStepsAsync(request.NodeId, definitionSteps, ct)` — throws if invalid
3. Call `WorkflowResolutionService.BuildApprovalChainAsync(employeeId, request.NodeId, definitionSteps, ct)` → returns `List<PlannedStepDto>`
4. Serialize to `PlannedStepsJson` (not PlannedChainJson)
5. Set `CurrentStepOrder = 1`
6. **Key rule**: If no managers at any step during build → entire creation fails with `NoActiveManagersAtStep`

---

## Phase G: Update ApproveRequest Command

### ApproveRequestCommandHandler.cs
1. Get request, deserialize `PlannedStepsJson` (not PlannedChainJson)
2. Validate `CurrentStepOrder` ≤ steps count
3. Validate current user (via `_currentUserService`) is one of the approvers for `CurrentStepOrder`
4. Advance: `CurrentStepOrder++`
5. If `CurrentStepOrder > steps.Count` → set `Status = Approved`, `CurrentStepOrder = 0`
6. Otherwise: move to next step (no CurrentApproverId)
7. Add history record

---

## Phase H: Update RejectRequest Command

### RejectRequestCommandHandler.cs
1. Get request
2. Set `Status = Rejected`
3. Set `CurrentStepOrder = 0` (or leave unchanged, status is the source of truth)
4. No need to track current approver post-rejection

---

## Phase I: Update Query Handlers

### GetRequestByIdQueryHandler.cs
- Deserialize `PlannedStepsJson` instead of `PlannedChainJson`
- Return step order info instead of current approver info

### GetPendingApprovalsQueryHandler.cs
- Use `CurrentStepOrder` to determine if request is pending
- Check if current user is approver for current step (deserialize PlannedStepsJson)

---

## Phase J: Update EF Configuration

### RequestConfiguration.cs (or RequestWorkflowConfiguration.cs)
- Add unique index on `(RequestDefinitionId, SortOrder)` for WorkflowStep
- Ensure PlannedStepsJson column is NVARCHAR(MAX)

---

## Phase K: Remove Old Code

### Deprecate IWorkflowService and WorkflowService
- Mark `[Obsolete]` attributes

### Remove from RequestDefinitionRepository:
- `AnyDefinitionUsingRoleAsync` method and interface

---

## Phase L: Generate Migration
```
dotnet ef migrations add OrgNodeBasedApprovalWorkflow
```

---

## Verification
1. Build passes (dotnet build)
2. All tests pass (dotnet test)
3. Create a request and verify PlannedStepsJson is populated correctly
4. Approve a request and verify CurrentStepOrder advances
5. Reject a request and verify status becomes Rejected
6. Verify self-approval is excluded (requester not in approvers list)
7. Verify creation fails if any workflow step has no active managers