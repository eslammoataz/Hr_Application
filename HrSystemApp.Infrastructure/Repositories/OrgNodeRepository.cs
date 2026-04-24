using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class OrgNodeRepository : Repository<OrgNode>, IOrgNodeRepository
{
    public OrgNodeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<OrgNode>> GetChildrenAsync(Guid? parentId, CancellationToken ct)
        => await _context.OrgNodes
            .AsNoTracking()
            .Where(n => n.ParentId == parentId)
            .Include(n => n.Assignments).ThenInclude(a => a.Employee)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OrgNode>> GetDescendantsAsync(Guid nodeId, CancellationToken ct)
    {
        var descendants = new List<OrgNode>();
        var toProcess = new Queue<Guid>();
        toProcess.Enqueue(nodeId);

        while (toProcess.Count > 0)
        {
            var currentId = toProcess.Dequeue();
            var children = await _context.OrgNodes
                .AsNoTracking()
                .Where(n => n.ParentId == currentId)
                .Select(n => n.Id)
                .ToListAsync(ct);

            foreach (var childId in children)
            {
                descendants.AddRange(await _context.OrgNodes
                    .AsNoTracking()
                    .Where(n => n.Id == childId)
                    .ToListAsync(ct));
                toProcess.Enqueue(childId);
            }
        }

        return descendants;
    }

    public async Task<bool> IsAncestorOfAsync(Guid ancestorId, Guid descendantId, CancellationToken ct)
    {
        var current = await _context.OrgNodes
            .AsNoTracking()
            .Where(n => n.Id == descendantId)
            .Select(n => new { n.ParentId })
            .FirstOrDefaultAsync(ct);

        while (current?.ParentId != null)
        {
            if (current.ParentId == ancestorId)
                return true;

            current = await _context.OrgNodes
                .AsNoTracking()
                .Where(n => n.Id == current.ParentId)
                .Select(n => new { n.ParentId })
                .FirstOrDefaultAsync(ct);
        }

        return false;
    }

    public async Task<int> GetChildCountAsync(Guid? parentId, CancellationToken ct)
        => await _context.OrgNodes.CountAsync(n => n.ParentId == parentId, ct);

    public async Task<OrgNode?> GetByIdWithChildrenAsync(Guid id, CancellationToken ct)
        => await _context.OrgNodes
            .AsNoTracking()
            .Include(n => n.Children)
            .Include(n => n.Assignments).ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<OrgNode>> GetRootNodesAsync(CancellationToken ct)
        => await _context.OrgNodes
            .AsNoTracking()
            .Where(n => n.ParentId == null)
            .Include(n => n.Assignments).ThenInclude(a => a.Employee)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OrgNode>> GetAncestorsAsync(Guid nodeId, CancellationToken ct)
    {
        // STEP 1: Get ancestor IDs using recursive CTE
        // PostgreSQL requires WITH RECURSIVE for self-referencing CTEs.
        // Table/column names are double-quoted to preserve case (EF Core creates them as "OrgNodes", "Id", etc.)
        // "IsDeleted" must be included in the projection so EF Core's global soft-delete filter
        // (WHERE NOT h."IsDeleted") can reference it on the outer subquery wrapper it generates.
        var ancestorIds = await _context.OrgNodes
            .FromSqlRaw(@"
            WITH RECURSIVE Ancestors AS (
                SELECT ""Id"", ""ParentId"", ""IsDeleted"", 0 AS ""Depth""
                FROM ""OrgNodes""
                WHERE ""Id"" = {0}

                UNION ALL

                SELECT p.""Id"", p.""ParentId"", p.""IsDeleted"", a.""Depth"" + 1
                FROM ""OrgNodes"" p
                INNER JOIN Ancestors a ON p.""Id"" = a.""ParentId""
            )
            SELECT ""Id"", ""IsDeleted"", ""Depth""
            FROM Ancestors
            WHERE ""Id"" != {0}
            ORDER BY ""Depth""
        ", nodeId)
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(ct);

        if (ancestorIds.Count == 0)
            return Array.Empty<OrgNode>();

        // STEP 2: Load full entities with navigation properties
        var ancestors = await _context.OrgNodes
            .AsNoTracking()
            .Where(n => ancestorIds.Contains(n.Id))
            .Include(n => n.Assignments)
                .ThenInclude(a => a.Employee)
            .ToListAsync(ct);

        // STEP 3: Preserve correct order
        var map = ancestors.ToDictionary(a => a.Id);

        var result = new List<OrgNode>(ancestorIds.Count);

        foreach (var id in ancestorIds)
        {
            if (map.TryGetValue(id, out var node))
                result.Add(node);
        }

        return result;
    }

    // public async Task<IReadOnlyList<OrgNode>> GetAncestorsAsync(Guid nodeId, CancellationToken ct)
    // {
    //     // Phase 1: Walk up collecting ParentId values (N lightweight queries)
    //     var ancestorIds = new List<Guid>();
    //     var currentParentId = await _context.OrgNodes
    //         .AsNoTracking()
    //         .Where(n => n.Id == nodeId)
    //         .Select(n => n.ParentId)
    //         .FirstOrDefaultAsync(ct);

    //     while (currentParentId.HasValue)
    //     {
    //         ancestorIds.Add(currentParentId.Value);
    //         currentParentId = await _context.OrgNodes
    //             .AsNoTracking()
    //             .Where(n => n.Id == currentParentId.Value)
    //             .Select(n => n.ParentId)
    //             .FirstOrDefaultAsync(ct);
    //     }

    //     if (ancestorIds.Count == 0)
    //         return new List<OrgNode>();

    //     // Phase 2: Fetch all ancestors with assignments in ONE query
    //     var ancestors = await _context.OrgNodes
    //         .AsNoTracking()
    //         .Where(n => ancestorIds.Contains(n.Id))
    //         .Include(n => n.Assignments).ThenInclude(a => a.Employee)
    //         .ToListAsync(ct);

    //     // Preserve order from immediate parent to root (ancestorIds is already in this order)
    //     return ancestorIds
    //         .Select(id => ancestors.First(a => a.Id == id))
    //         .ToList();
    // }

    public async Task<IReadOnlyList<OrgNode>> GetAncestorChainAsync(Guid startNodeId, Guid targetRootId, CancellationToken ct)
    {
        // Phase 1: Walk up collecting ParentId values (N lightweight queries)
        var ancestorIds = new List<Guid>();
        var currentParentId = await _context.OrgNodes
            .AsNoTracking()
            .Where(n => n.Id == startNodeId)
            .Select(n => n.ParentId)
            .FirstOrDefaultAsync(ct);

        while (currentParentId.HasValue && currentParentId.Value != targetRootId)
        {
            ancestorIds.Add(currentParentId.Value);
            currentParentId = await _context.OrgNodes
                .AsNoTracking()
                .Where(n => n.Id == currentParentId.Value)
                .Select(n => n.ParentId)
                .FirstOrDefaultAsync(ct);
        }

        if (ancestorIds.Count == 0)
            return Array.Empty<OrgNode>();

        // Phase 2: Fetch all ancestors with assignments in one query
        var ancestors = await _context.OrgNodes
            .AsNoTracking()
            .Where(n => ancestorIds.Contains(n.Id))
            .Include(n => n.Assignments)
                .ThenInclude(a => a.Employee)
            .ToListAsync(ct);

        // Preserve immediate-parent-to-root ordering from ancestorIds
        var map = ancestors.ToDictionary(a => a.Id);
        var result = new List<OrgNode>(ancestorIds.Count);
        foreach (var id in ancestorIds)
        {
            if (map.TryGetValue(id, out var node))
                result.Add(node);
        }

        return result;
    }

    public async Task<OrgNode> GetRootNodeAsync(Guid nodeId, CancellationToken ct = default)
    {
        var node = await _context.OrgNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == nodeId, ct);

        if (node is null)
            throw new InvalidOperationException($"Node {nodeId} not found.");

        while (node.ParentId.HasValue)
        {
            node = await _context.OrgNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == node.ParentId.Value, ct);

            if (node is null)
                throw new InvalidOperationException("Parent node not found while resolving root.");
        }

        return node;
    }

    public async Task<Dictionary<Guid, OrgNode>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        if (ids is null) return new Dictionary<Guid, OrgNode>();
        var idList = ids.ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, OrgNode>();

        var nodes = await _context.OrgNodes
            .AsNoTracking()
            .Where(n => idList.Contains(n.Id))
            .Include(n => n.Assignments).ThenInclude(a => a.Employee)
            .ToListAsync(ct);

        return nodes.ToDictionary(n => n.Id);
    }
}
