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
    /// <summary>
/// Finds the root node of the organizational tree that contains the specified node.
/// </summary>
/// <param name="nodeId">The identifier of the node whose containing root should be retrieved.</param>
/// <param name="ct">A cancellation token to cancel the operation.</param>
/// <returns>The root <see cref="OrgNode"/> for the tree containing the specified node, or <c>null</c> if the node does not exist.</returns>
    Task<OrgNode> GetRootNodeAsync(Guid nodeId, CancellationToken ct = default);

    /// <summary>
    /// Gets multiple nodes by their IDs in a single query.
    /// <summary>
/// Retrieves OrgNode instances for the given collection of IDs and returns them keyed by their ID.
/// </summary>
/// <param name="ids">The collection of node IDs to retrieve.</param>
/// <returns>A dictionary mapping each found node's Guid to its corresponding OrgNode; IDs with no matching node are not included.</returns>
    Task<Dictionary<Guid, OrgNode>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);
}
