using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IOrgNodeAssignmentRepository : IRepository<OrgNodeAssignment>
{
    Task<bool> ExistsAsync(Guid orgNodeId, Guid employeeId, CancellationToken ct);
    Task<IReadOnlyList<OrgNodeAssignment>> GetByNodeAsync(Guid orgNodeId, CancellationToken ct);
    Task<OrgNodeAssignment?> GetByNodeAndEmployeeAsync(Guid orgNodeId, Guid employeeId, CancellationToken ct);
    Task<IReadOnlyList<OrgNodeAssignment>> GetByEmployeeAsync(Guid employeeId, CancellationToken ct);
    Task<OrgNodeAssignment?> GetByEmployeeWithNodeAsync(Guid employeeId, CancellationToken ct);

    /// <summary>
    /// Gets all employees assigned to a node with OrgRole = Manager (excludes IsDeleted).
    /// </summary>
    Task<IReadOnlyList<Employee>> GetManagersByNodeAsync(Guid orgNodeId, CancellationToken ct);

    /// <summary>
    /// Gets managers for multiple nodes in a single query. Returns a dictionary mapping OrgNodeId to its managers.
    /// </summary>
    Task<Dictionary<Guid, IReadOnlyList<Employee>>> GetManagersByNodesAsync(IEnumerable<Guid> orgNodeIds, CancellationToken ct);

    /// <summary>
    /// Checks if an employee has OrgRole = Manager at a specific node (excludes IsDeleted).
    /// </summary>
    Task<bool> IsManagerAtNodeAsync(Guid employeeId, Guid orgNodeId, CancellationToken ct = default);
}
