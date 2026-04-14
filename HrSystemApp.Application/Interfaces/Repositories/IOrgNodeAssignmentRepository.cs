using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IOrgNodeAssignmentRepository : IRepository<OrgNodeAssignment>
{
    Task<bool> ExistsAsync(Guid orgNodeId, Guid employeeId, CancellationToken ct);
    Task<IReadOnlyList<OrgNodeAssignment>> GetByNodeAsync(Guid orgNodeId, CancellationToken ct);
    Task<OrgNodeAssignment?> GetByNodeAndEmployeeAsync(Guid orgNodeId, Guid employeeId, CancellationToken ct);
}