using System.Linq.Expressions;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IRequestDefinitionRepository : IRepository<RequestDefinition>
{
    Task<RequestDefinition?> GetByTypeAsync(Guid companyId, RequestType requestType, CancellationToken cancellationToken = default);
    Task<List<RequestDefinition>> GetByCompanyAsync(Guid companyId, RequestType? type = null, CancellationToken cancellationToken = default);

    [Obsolete("Role-based workflow is deprecated. Use OrgNode-based workflow steps.")]
    Task<bool> AnyDefinitionUsingRoleAsync(Guid companyId, UserRole role, CancellationToken ct = default);

    Task<bool> IsRoleInUseAsync(Guid companyRoleId, CancellationToken ct = default);
}
