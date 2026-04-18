# Plan: Add Optional `Type` Field to OrgNode

## Context
Add an optional `Type` string field to OrgNode for display/filtering purposes. Type is purely informational — no business logic, no validation, no enum, no constraints.

---

## Files to Modify

### 1. Domain Model
**`HrSystemApp.Domain/Models/OrgNode.cs`**
- Add: `public string? Type { get; set; }`

---

### 2. DTOs
**`HrSystemApp.Application/DTOs/OrgNodes/OrgNodeResponse.cs`**
- Add: `string? Type`

**`HrSystemApp.Application/DTOs/OrgNodes/OrgNodeDetailsResponse.cs`**
- Add: `string? Type`

**`HrSystemApp.Application/DTOs/OrgNodes/OrgNodeTreeResponse.cs`**
- Add: `string? Type`

**`HrSystemApp.Application/DTOs/OrgNodes/OrgNodeChildResponse.cs`**
- Add: `string? Type`

---

### 3. Requests
**`HrSystemApp.Application/DTOs/OrgNodes/CreateOrgNodeRequest.cs`**
- Add: `string? Type`

**`HrSystemApp.Application/DTOs/OrgNodes/UpdateOrgNodeRequest.cs`**
- Add: `string? Type`

---

### 4. Commands
**`CreateOrgNodeCommand.cs`**
- Add: `string? Type` property

**`CreateOrgNodeCommandHandler.cs`**
- In `Handle()`: normalize `Type = request.Type?.Trim().ToLower()` before saving

**`UpdateOrgNodeCommand.cs`**
- Add: `string? Type` property

**`UpdateOrgNodeCommandHandler.cs`**
- In `Handle()`: normalize `Type = request.Type?.Trim().ToLower()` before updating (only if provided)

---

### 5. EF Configuration
**`HrSystemApp.Infrastructure/Data/Configurations/OrgNodeConfiguration.cs`**
- Add: `builder.Property(n => n.Type).HasMaxLength(50).IsRequired(false);`

---

## Query Filtering by Type

### 1. Query Request
**`GetOrgNodeTreeQuery.cs`**
- Add: `public string? Type { get; set; }`

---

### 2. Handler Filter Logic
**`GetOrgNodeTreeQueryHandler.cs`**
- In `Handle()`: apply `request.Type` filter when non-null:
```csharp
if (!string.IsNullOrWhiteSpace(request.Type))
{
    var normalizedType = request.Type.Trim().ToLower();
    // Filter startNodes by Type
}
```

Note: Since the tree query uses `GetChildrenAsync` / `GetRootNodesAsync` (no direct IQueryable), the filter should be applied to the initial `startNodes` result:
```csharp
var startNodes = request.ParentId.HasValue
    ? await _unitOfWork.OrgNodes.GetChildrenAsync(request.ParentId, cancellationToken)
    : await _unitOfWork.OrgNodes.GetRootNodesAsync(cancellationToken);

if (!string.IsNullOrWhiteSpace(request.Type))
{
    var normalizedType = request.Type.Trim().ToLower();
    startNodes = startNodes.Where(n => n.Type == normalizedType).ToList();
}
```

Apply same filter in recursive `BuildTreeAsync` for child nodes if Type filtering is needed at deeper levels. Alternatively, filter only at root level (mobile app can request subtrees separately).

---

## Verification
1. Build passes
2. All existing tests pass
3. Can create OrgNode with `Type = "engineering"` → stored as `"engineering"`
4. Can create OrgNode without Type → Type is null
5. `GET /api/orgnodes?type=engineering&depth=2` returns only nodes with Type="engineering"
6. Existing endpoints work unchanged (Type is optional)

---

## Constraints
- DO NOT create enum for Type
- DO NOT add business logic based on Type
- DO NOT add validation or uniqueness constraint
- DO NOT modify delete behavior
- DO NOT apply Type filter to single-node fetch by ID
