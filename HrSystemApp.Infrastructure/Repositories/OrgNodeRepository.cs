using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
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
            .Include(n => n.Level)
            .Include(n => n.Assignments).ThenInclude(a => a.Employee)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OrgNode>> GetByEntityAsync(Guid entityId, OrgEntityType type, CancellationToken ct)
        => await _context.OrgNodes
            .AsNoTracking()
            .Where(n => n.EntityId == entityId && n.EntityType == type)
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
                    .Include(n => n.Assignments)
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

    public async Task<bool> IsLinkedToEntityAsync(Guid entityId, OrgEntityType type, CancellationToken ct)
        => await _context.OrgNodes
            .AsNoTracking()
            .AnyAsync(n => n.EntityId == entityId && n.EntityType == type, ct);

    public async Task<int> GetChildCountAsync(Guid? parentId, CancellationToken ct)
        => await _context.OrgNodes.CountAsync(n => n.ParentId == parentId, ct);

    public async Task<OrgNode?> GetByIdWithChildrenAsync(Guid id, CancellationToken ct)
        => await _context.OrgNodes
            .AsNoTracking()
            .Include(n => n.Children)
            .Include(n => n.Assignments).ThenInclude(a => a.Employee)
            .Include(n => n.Level)
            .FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<OrgNode>> GetRootNodesAsync(CancellationToken ct)
        => await _context.OrgNodes
            .AsNoTracking()
            .Where(n => n.ParentId == null)
            .Include(n => n.Level)
            .Include(n => n.Assignments).ThenInclude(a => a.Employee)
            .ToListAsync(ct);
}