using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IOrgNodeRepository : IRepository<OrgNode>
{
    Task<IReadOnlyList<OrgNode>> GetChildrenAsync(Guid? parentId, CancellationToken ct);
    Task<IReadOnlyList<OrgNode>> GetDescendantsAsync(Guid nodeId, CancellationToken ct);
    Task<bool> IsAncestorOfAsync(Guid ancestorId, Guid descendantId, CancellationToken ct);
    Task<int> GetChildCountAsync(Guid? parentId, CancellationToken ct);
    Task<OrgNode?> GetByIdWithChildrenAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<OrgNode>> GetRootNodesAsync(CancellationToken ct);

    /// <summary>
    /// Gets all ancestors from the immediate parent up to the root.
    /// Ordered from nearest parent to root (depth ascending).
    /// </summary>
    Task<IReadOnlyList<OrgNode>> GetAncestorsAsync(Guid nodeId, CancellationToken ct);

    /// <summary>
    /// Gets the ancestor chain from startNode up to (but not including) targetRootId.
    /// Used when building approval chains that stop at a certain root.
    /// </summary>
    Task<IReadOnlyList<OrgNode>> GetAncestorChainAsync(Guid startNodeId, Guid targetRootId, CancellationToken ct);

    /// <summary>
    /// Gets the root node of the tree containing the given node.
    /// </summary>
    Task<OrgNode> GetRootNodeAsync(Guid nodeId, CancellationToken ct = default);

    /// <summary>
    /// Gets multiple nodes by their IDs in a single query.
    /// </summary>
    Task<Dictionary<Guid, OrgNode>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);
}
