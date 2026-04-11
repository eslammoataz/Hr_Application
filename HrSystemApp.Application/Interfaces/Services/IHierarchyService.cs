using HrSystemApp.Application.DTOs.Hierarchy;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Services;

/// <summary>
/// Service responsible for reading and validating the company organizational hierarchy.
/// </summary>
public interface IHierarchyService
{
    /// <summary>
    /// Returns all UserRoles configured in the company's hierarchy positions, ordered by SortOrder.
    /// </summary>
    Task<List<UserRole>> GetAvailableRolesAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>
    /// Returns true if ALL provided roles are present in the company's configured hierarchy.
    /// Used to validate Request Definition workflow steps.
    /// </summary>
    Task<bool> AreRolesValidForCompanyAsync(Guid companyId, IEnumerable<UserRole> roles, CancellationToken ct = default);

    /// <summary>
    /// Walks up the organizational tree from the employee and returns the ordered chain
    /// of head-employees (TeamLeader → UnitLeader → DeptManager → VP → CEO).
    /// <summary>
/// Gets the upward leadership chain for the specified employee.
/// </summary>
/// <param name="employeeId">The identifier of the employee whose leadership chain will be retrieved.</param>
/// <returns>An ordered list of employees representing the chain of supervisors from the employee's immediate leader up to the top-level executive (e.g., TeamLeader → UnitLeader → DeptManager → VP → CEO).</returns>
    Task<List<Employee>> GetEmployeeHierarchyPathAsync(Guid employeeId, CancellationToken ct = default);

    /// <summary>
    /// Implements the standardized "Zig-Zag" organizational discovery.
    /// Organizations (Dept/Unit/Team) expand to their leader.
    /// Leaders expand to their direct reports AND organizations they lead.
    /// <summary>
/// Discovers child hierarchy nodes for a given parent using the standardized "Zig-Zag" traversal rule.
/// </summary>
/// <param name="companyId">The company whose hierarchy is being traversed.</param>
/// <param name="parentId">The identifier of the parent node to expand.</param>
/// <param name="parentType">The node type of the parent (e.g., "Department", "Unit", "Team", "Leader"); determines whether the parent expands to its leader or to its direct reports and led organizations.</param>
/// <returns>A list of child nodes represented as tuples where each tuple contains the child's Id and its Type.</returns>
    Task<List<(Guid Id, string Type)>> GetHierarchyChildrenAsync(Guid companyId, Guid parentId, string parentType, CancellationToken ct = default);

    /// <summary>
    /// Fetches metadata (Name, Position, Role, hasChildren) for a list of mixed node types.
    /// Used for batching to prevent N+1 queries during tree mapping.
    /// <summary>
/// Retrieves metadata for a collection of hierarchy nodes of mixed types to support batched lookups.
/// </summary>
/// <param name="nodes">A collection of nodes represented as tuples where `Id` is the node Guid and `Type` is the node kind (e.g., "Department", "Team", "Employee").</param>
/// <param name="ct">Cancellation token to cancel the operation.</param>
/// <returns>A dictionary mapping each node's Guid to its corresponding <see cref="HierarchyNodeMetadata"/>.</returns>
    Task<Dictionary<Guid, HierarchyNodeMetadata>> GetNodesMetadataAsync(IEnumerable<(Guid Id, string Type)> nodes, CancellationToken ct = default);
}
