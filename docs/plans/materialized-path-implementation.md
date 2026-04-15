# Plan: Materialized Path for OrgNode Hierarchy

## Context

The current `GetAncestorsAsync` implementation uses N+1 queries - one database query per hierarchy level to walk up the parent chain. For a 5-level hierarchy, this means 6 database round-trips (5 to walk up + 1 to fetch nodes with Include).

**Solution:** Add a materialized `AncestorPath` column that stores all ancestor IDs as a string. This allows fetching all ancestors in exactly 2 queries.

---

## Why This Approach?

| Approach | Queries | Round-trips | EF Tracking | DB Portable |
|----------|---------|-------------|-------------|-------------|
| Current (N+1 loop) | N + 1 | N + 1 | Yes | Yes |
| Recursive CTE | 1 | 1 | No (raw SQL) | No (PostgreSQL) |
| **Materialized Path** | 2 | 2 | Yes | Yes |

Materialized Path gives us near-CTE performance while:
- Staying within EF Core's change tracking
- Working with any database (not PostgreSQL-specific)
- Maintaining type safety

---

## Path Format

```
AncestorPath = "/{ancestorId1}/{ancestorId2}/{ancestorId3}/"
```

**Examples:**

```
Hierarchy:
                    [Company]           Path = "/"
                        |
                    [Department A]      Path = "/{companyId}/"
                        |
                    [Unit 1]           Path = "/{companyId}/{deptAId}/"
                        |
                    [Team Alpha]       Path = "/{companyId}/{deptAId}/{unit1Id}/"
```

**Key rules:**
1. Root node's path = "/"
2. Child node's path = parent's path + parent's ID + "/"
3. Path always starts and ends with "/"
4. IDs are stored as strings (Guid.ToString())

---

## Files to Modify

### 1. Domain Model - OrgNode.cs

**Path:** `HrSystemApp.Domain/Models/OrgNode.cs`

**Change:**
```csharp
public class OrgNode : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public string? Type { get; set; }

    // NEW: Materialized path of ancestor IDs (format: "/id1/id2/id3/")
    // Stores all ancestors from root to immediate parent
    public string? AncestorPath { get; set; }

    // Navigation
    public OrgNode? Parent { get; set; }
    public ICollection<OrgNode> Children { get; set; } = new List<OrgNode>();
    public ICollection<OrgNodeAssignment> Assignments { get; set; } = new List<OrgNodeAssignment>();
}
```

**Why this works:**
- `AncestorPath` stores ALL ancestors, not just immediate parent
- When we need ancestors, we parse the path and fetch in 1 query
- Path is updated on create and when moving nodes

---

### 2. EF Configuration - OrgNodeConfiguration.cs

**Path:** `HrSystemApp.Infrastructure/Data/Configurations/OrgNodeConfiguration.cs`

**Add after existing properties:**
```csharp
// Materialized path for fast ancestor queries (format: "/id1/id2/")
builder.Property(n => n.AncestorPath)
    .HasMaxLength(2000)  // Max depth ~25 with Guid strings
    .IsRequired(false);   // Can be null for root nodes, then treat as "/"
```

**Why 2000 chars:**
- Guid string length = 36 chars
- Max path with 25 levels = 25 * (36 + 1) + 1 = 926 chars
- 2000 gives plenty of headroom for safety

---

### 3. CreateOrgNodeCommandHandler.cs

**Path:** `HrSystemApp.Application/Features/OrgNodes/Commands/CreateOrgNode/CreateOrgNodeCommandHandler.cs`

**Current code (lines 43-49):**
```csharp
var node = new OrgNode
{
    Name = request.Name,
    ParentId = request.ParentId,
    Type = request.Type?.Trim().ToLower()
};
```

**New code:**
```csharp
// Calculate AncestorPath based on parent
string? ancestorPath = null;
if (request.ParentId.HasValue)
{
    var parent = await _unitOfWork.OrgNodes.GetByIdAsync(request.ParentId.Value, cancellationToken);
    if (parent == null)
    {
        _logger.LogWarning("CreateOrgNode failed: Parent {ParentId} not found.", request.ParentId);
        return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
    }
    // Path = parent's path + parent's ID
    ancestorPath = $"{parent.AncestorPath}{parent.Id}/";
}
else
{
    // Root node has path "/"
    ancestorPath = "/";
}

var node = new OrgNode
{
    Name = request.Name,
    ParentId = request.ParentId,
    Type = request.Type?.Trim().ToLower(),
    AncestorPath = ancestorPath
};
```

**Edge cases handled:**
- Root node (no parent): Path = "/"
- Child of root: Path = "/{rootId}/"
- Child of child: Path = "/{rootId}/{parentId}/"

---

### 4. UpdateOrgNodeCommandHandler.cs

**Path:** `HrSystemApp.Application/Features/OrgNodes/Commands/UpdateOrgNode/UpdateOrgNodeCommandHandler.cs`

**This is the most complex change.** When a node moves to a new parent, we need to:

1. Calculate the new path for the moved node
2. Update the moved node's path
3. Update ALL descendants' paths (they all shift)

**Current code (lines 65-68):**
```csharp
node.Name = request.Name;
node.ParentId = request.ParentId;
node.Type = request.Type?.Trim().ToLower();
```

**New code:**
```csharp
// Check if ParentId is changing (node move)
if (request.ParentId != node.ParentId)
{
    _logger.LogInformation("Node {NodeId} is moving from parent {OldParentId} to {NewParentId}",
        node.Id, node.ParentId, request.ParentId);

    // Calculate new AncestorPath
    string newAncestorPath;
    if (request.ParentId.HasValue)
    {
        var newParent = await _unitOfWork.OrgNodes.GetByIdAsync(request.ParentId.Value, cancellationToken);
        if (newParent == null)
        {
            _logger.LogWarning("UpdateOrgNode failed: New parent {ParentId} not found.", request.ParentId);
            return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
        }
        newAncestorPath = $"{newParent.AncestorPath}{newParent.Id}/";
    }
    else
    {
        newAncestorPath = "/";
    }

    var oldAncestorPath = node.AncestorPath ?? "/";
    var oldPathPrefix = $"{oldAncestorPath}{node.Id}/";
    var newPathPrefix = $"{newAncestorPath}{node.Id}/";

    // Update this node's path
    node.AncestorPath = newAncestorPath;

    // Update ALL descendants' paths
    // A descendant's path starts with the old prefix, replace with new prefix
    var descendants = await _unitOfWork.OrgNodes.GetDescendantsWithPathsAsync(node.Id, oldPathPrefix, cancellationToken);
    foreach (var descendant in descendants)
    {
        // Replace old prefix with new prefix
        descendant.AncestorPath = newPathPrefix + descendant.AncestorPath.Substring(oldPathPrefix.Length);
    }

    _logger.LogInformation("Updated paths for {Count} descendants", descendants.Count);
}

// Update other properties
node.Name = request.Name;
node.ParentId = request.ParentId;
node.Type = request.Type?.Trim().ToLower();
```

**New repository method needed (see section 6).**

---

### 5. BulkSetupOrgNodesCommandHandler.cs

**Path:** `HrSystemApp.Application/Features/OrgNodes/Commands/BulkSetupOrgNodes/BulkSetupOrgNodesCommandHandler.cs`

**This is the most complex bulk operation.** We need to track paths by TempId during creation.

**Current approach:** Uses `tempIdToNode` to map TempId -> created node

**New approach:** Add `tempIdToPath` to track AncestorPath by TempId

**Changes:**

1. Add tracking dictionary:
```csharp
var tempIdToPath = new Dictionary<string, string>();
```

2. When creating root nodes (after line 59):
```csharp
tempIdToNode[root.TempId] = node;
tempIdToPath[root.TempId] = "/";  // Root path
```

3. When creating child nodes (after line 97):
```csharp
// Calculate path from parent's TempId
string parentPath;
if (string.IsNullOrEmpty(nodeDto.ParentTempId))
{
    parentPath = "/";
}
else
{
    parentPath = tempIdToPath[nodeDto.ParentTempId];
}

var ancestorPath = $"{parentPath}{node.Id}/";
tempIdToNode[nodeDto.TempId] = node;
tempIdToPath[nodeDto.TempId] = ancestorPath;
```

4. Set path when creating node:
```csharp
var node = new OrgNode
{
    Name = nodeDto.Name,
    Type = nodeDto.Type?.Trim().ToLower(),
    ParentId = parentId,
    AncestorPath = ancestorPath  // NEW
};
```

---

### 6. OrgNodeRepository.cs

**Path:** `HrSystemApp.Infrastructure/Repositories/OrgNodeRepository.cs`

#### 6a. GetAncestorsAsync - SIMPLIFIED (most important)

**Current code (lines 87-121):**
```csharp
public async Task<IReadOnlyList<OrgNode>> GetAncestorsAsync(Guid nodeId, CancellationToken ct)
{
    var ancestors = new List<OrgNode>();
    var current = await _context.OrgNodes
        .AsNoTracking()
        .Where(n => n.Id == nodeId)
        .Select(n => new { n.ParentId })
        .FirstOrDefaultAsync(ct);

    while (current?.ParentId != null)
    {
        var parent = await _context.OrgNodes
            .AsNoTracking()
            .Where(n => n.Id == current.ParentId)
            .Include(n => n.Assignments).ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(ct);

        if (parent == null) break;

        ancestors.Add(parent);
        current = new { parent.ParentId };
    }

    return ancestors;
}
```

**New code:**
```csharp
public async Task<IReadOnlyList<OrgNode>> GetAncestorsAsync(Guid nodeId, CancellationToken ct)
{
    // STEP 1: Get the node's AncestorPath (1 query)
    var path = await _context.OrgNodes
        .AsNoTracking()
        .Where(n => n.Id == nodeId)
        .Select(n => n.AncestorPath)
        .FirstOrDefaultAsync(ct);

    if (string.IsNullOrEmpty(path) || path == "/")
        return new List<OrgNode>();

    // Parse IDs from path: "/id1/id2/id3/" -> [id1, id2, id3]
    var ancestorIds = path
        .Split('/', StringSplitOptions.RemoveEmptyEntries)
        .Select(s => Guid.Parse(s))
        .ToList();

    if (ancestorIds.Count == 0)
        return new List<OrgNode>();

    // STEP 2: Fetch all ancestors in ONE query (1 query)
    var ancestors = await _context.OrgNodes
        .AsNoTracking()
        .Where(n => ancestorIds.Contains(n.Id))
        .Include(n => n.Assignments).ThenInclude(a => a.Employee)
        .ToListAsync(ct);

    // Preserve order from immediate parent to root (path order)
    return ancestorIds
        .Select(id => ancestors.First(a => a.Id == id))
        .ToList();
}
```

**Performance improvement:**
- Before: N queries with joins + 1 fetch = N+1 queries
- After: 1 path query + 1 fetch = 2 queries

#### 6b. GetDescendantsWithPathsAsync - NEW METHOD

**Needed by UpdateOrgNodeCommandHandler to update descendant paths after a move.**

```csharp
/// <summary>
/// Gets all descendants of a node whose paths start with the given prefix.
/// Used for updating descendant paths after a node move.
/// </summary>
public async Task<IReadOnlyList<OrgNode>> GetDescendantsWithPathsAsync(
    Guid nodeId,
    string pathPrefix,
    CancellationToken ct)
{
    // Find all nodes whose AncestorPath starts with the moved node's path
    // This means they are descendants of the moved node
    return await _context.OrgNodes
        .Where(n => n.AncestorPath != null
                    && n.AncestorPath.StartsWith(pathPrefix)
                    && n.Id != nodeId)  // Exclude the node itself
        .ToListAsync(ct);
}
```

#### 6c. GetAncestorChainAsync - SIMPLIFIED

**Current code (lines 123-157):**
```csharp
public async Task<IReadOnlyList<OrgNode>> GetAncestorChainAsync(Guid startNodeId, Guid targetRootId, CancellationToken ct)
{
    var ancestors = new List<OrgNode>();
    var current = await _context.OrgNodes
        .AsNoTracking()
        .Where(n => n.Id == startNodeId)
        .Select(n => new { n.ParentId })
        .FirstOrDefaultAsync(ct);

    while (current?.ParentId != null && current.ParentId != targetRootId)
    {
        var parent = await _context.OrgNodes
            .AsNoTracking()
            .Where(n => n.Id == current.ParentId)
            .Include(n => n.Assignments).ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(ct);

        if (parent == null) break;

        ancestors.Add(parent);
        current = new { parent.ParentId };
    }

    return ancestors;
}
```

**New code:**
```csharp
public async Task<IReadOnlyList<OrgNode>> GetAncestorChainAsync(Guid startNodeId, Guid targetRootId, CancellationToken ct)
{
    // Get path of the start node
    var path = await _context.OrgNodes
        .Where(n => n.Id == startNodeId)
        .Select(n => n.AncestorPath)
        .FirstOrDefaultAsync(ct);

    if (string.IsNullOrEmpty(path))
        return new List<OrgNode>();

    // Parse IDs and reverse (we want from startNode's ancestors up to, but not including, targetRootId)
    var allAncestorIds = path
        .Split('/', StringSplitOptions.RemoveEmptyEntries)
        .Select(s => Guid.Parse(s))
        .ToList();

    // Find where targetRootId appears in the path
    var rootIndex = allAncestorIds.IndexOf(targetRootId);
    if (rootIndex == -1)
        return new List<OrgNode>();  // targetRootId not in ancestor chain

    // Get only ancestors up to (but not including) targetRootId
    var ancestorIds = allAncestorIds.Take(rootIndex).ToList();
    if (ancestorIds.Count == 0)
        return new List<OrgNode>();

    // Fetch all in one query
    var ancestors = await _context.OrgNodes
        .Where(n => ancestorIds.Contains(n.Id))
        .Include(n => n.Assignments).ThenInclude(a => a.Employee)
        .ToListAsync(ct);

    // Return in order from immediate parent to root (reverse of how they appear in path, which is root-to-parent)
    return ancestorIds
        .Select(id => ancestors.First(a => a.Id == id))
        .ToList();
}
```

#### 6d. GetRootNodeAsync - SIMPLIFIED

**Current code uses loop. New code can use path:**

```csharp
public async Task<OrgNode> GetRootNodeAsync(Guid nodeId, CancellationToken ct)
{
    var path = await _context.OrgNodes
        .Where(n => n.Id == nodeId)
        .Select(n => n.AncestorPath)
        .FirstOrDefaultAsync(ct);

    if (string.IsNullOrEmpty(path) || path == "/")
    {
        // This node is the root
        return await _context.OrgNodes
            .Where(n => n.Id == nodeId)
            .Include(n => n.Assignments).ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Node {nodeId} not found.");
    }

    // First ID in path is the root
    var rootId = Guid.Parse(path.Split('/', StringSplitOptions.RemoveEmptyEntries).First());

    return await _context.OrgNodes
        .Where(n => n.Id == rootId)
        .Include(n => n.Assignments).ThenInclude(a => a.Employee)
        .FirstOrDefaultAsync(ct)
        ?? throw new InvalidOperationException($"Root node {rootId} not found.");
}
```

---

### 7. Migration

**Command:**
```bash
dotnet ef migrations add AddAncestorPathToOrgNode --project HrSystemApp.Infrastructure --startup-project HrSystemApp.Api --output-dir Migrations
```

**Generated migration should:**
1. Add `AncestorPath` column (nvarchar(2000), nullable)
2. Set default value for existing rows

---

## Verification Steps

### 1. Build
```bash
dotnet build
```

### 2. Test AncestorPath Creation

**Create hierarchy:**
```
Company (root)
  └── Department A
        └── Unit 1
              └── Team Alpha
```

**Verify paths:**
- Company: `AncestorPath = "/"`
- Department A: `AncestorPath = "/{companyId}/"`
- Unit 1: `AncestorPath = "/{companyId}/{deptAId}/"`
- Team Alpha: `AncestorPath = "/{companyId}/{deptAId}/{unit1Id}/"`

### 3. Test GetAncestorsAsync

**Before fix:** 4 queries + 1 fetch = 5 queries
**After fix:** 1 path query + 1 fetch = 2 queries

Verify with SQL profiler or logging.

### 4. Test Node Move

**Move Team Alpha from Unit 1 to Unit 2**

**Before:**
- Team Alpha: Path = "/{companyId}/{deptAId}/{unit1Id}/"
- SubTeam 1 (child of Team Alpha): Path = "/{companyId}/{deptAId}/{unit1Id}/{teamAlphaId}/"

**After:**
- Team Alpha: Path = "/{companyId}/{deptAId}/{unit2Id}/"
- SubTeam 1: Path = "/{companyId}/{deptAId}/{unit2Id}/{teamAlphaId}/"

**Verify:**
- Team Alpha's path updated
- SubTeam 1's path updated
- No other nodes affected

### 5. Test Bulk Setup

Create bulk hierarchy and verify all paths are correct.

---

## Edge Cases

### 1. Root Node Creation
```csharp
// When ParentId is null
AncestorPath = "/";
```

### 2. Moving to Root
```csharp
// When new ParentId is null
AncestorPath = "/";
```

### 3. Moving to Deeper Level
```csharp
// New parent deeper in tree
AncestorPath = newParent.AncestorPath + newParent.Id + "/";
```

### 4. Self-Approval Prevention in Workflow

The `WorkflowResolutionService.BuildApprovalChainAsync` still works correctly because:
- It calls `GetAncestorsAsync` to get the list of ancestors
- It then uses `IsManagerAtNodeAsync` to check if employee is manager at own node
- The path is just for efficient querying, not business logic

### 5. Cycle Detection Still Works

`UpdateOrgNodeCommandHandler` still uses `IsAncestorOfAsync` to check for cycles. The path is for queries, not cycle detection.

---

## Performance Impact

### GetAncestorsAsync (most important)

**Before (for 5-level hierarchy):**
- Query 1: SELECT ParentId FROM OrgNodes WHERE Id = @nodeId
- Query 2: SELECT * FROM OrgNodes WHERE Id = @parentId1 (with Include)
- Query 3: SELECT * FROM OrgNodes WHERE Id = @parentId2 (with Include)
- Query 4: SELECT * FROM OrgNodes WHERE Id = @parentId3 (with Include)
- Query 5: SELECT * FROM OrgNodes WHERE Id = @parentId4 (with Include)
- Query 6: SELECT * FROM OrgNodes WHERE Id IN (...) (fetch all with Include)

**Total: 6 queries, 6 round-trips**

**After:**
- Query 1: SELECT AncestorPath FROM OrgNodes WHERE Id = @nodeId
- Query 2: SELECT * FROM OrgNodes WHERE Id IN (...) (with Include)

**Total: 2 queries, 2 round-trips**

### Bulk Operations

Bulk setup path calculation is O(1) per node - just string concatenation. No additional queries needed.

### Move Operation

Moving a node requires:
1. Get new parent's path (1 query)
2. Update node's path (1 query)
3. Find descendants by path prefix (1 query)
4. Update each descendant (N queries, but N is typically small)

**Note:** Move is more expensive than before, but moves are less frequent than reads.

---

## Rollback Considerations

If we ever need to remove the materialized path:
1. Remove `AncestorPath` column
2. Delete `GetDescendantsWithPathsAsync` method
3. Revert `GetAncestorsAsync` to the loop-based approach
4. Remove path-setting logic from Create/Update handlers

The path is a performance optimization, not a structural requirement. The hierarchy would still work without it (just slower).

---

## Summary

| Component | Change |
|-----------|--------|
| OrgNode model | + AncestorPath column |
| OrgNodeConfiguration | + Column mapping |
| CreateOrgNodeHandler | + Set AncestorPath |
| UpdateOrgNodeHandler | + Update own path + descendant paths |
| BulkSetupOrgNodesHandler | + Track paths by TempId |
| GetAncestorsAsync | Simplified to 2 queries |
| GetAncestorChainAsync | Simplified using path |
| GetRootNodeAsync | Simplified using path |
| GetDescendantsWithPathsAsync | **NEW** - for move operations |

**Net result:** Most hierarchy queries go from N+1 queries to 2 queries, while keeping full EF Core functionality and database portability.
