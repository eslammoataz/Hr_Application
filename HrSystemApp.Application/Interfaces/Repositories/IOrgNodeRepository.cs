using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IOrgNodeRepository : IRepository<OrgNode>
{
    Task<IReadOnlyList<OrgNode>> GetChildrenAsync(Guid? parentId, CancellationToken ct);
    Task<IReadOnlyList<OrgNode>> GetByEntityAsync(Guid entityId, OrgEntityType type, CancellationToken ct);
    Task<IReadOnlyList<OrgNode>> GetDescendantsAsync(Guid nodeId, CancellationToken ct);
    Task<bool> IsAncestorOfAsync(Guid ancestorId, Guid descendantId, CancellationToken ct);
    Task<bool> IsLinkedToEntityAsync(Guid entityId, OrgEntityType type, CancellationToken ct);
    Task<int> GetChildCountAsync(Guid? parentId, CancellationToken ct);
    Task<OrgNode?> GetByIdWithChildrenAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<OrgNode>> GetRootNodesAsync(CancellationToken ct);
}