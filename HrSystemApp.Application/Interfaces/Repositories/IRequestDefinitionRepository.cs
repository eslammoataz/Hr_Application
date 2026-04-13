using System.Linq.Expressions;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IRequestDefinitionRepository : IRepository<RequestDefinition>
{
    Task<RequestDefinition?> GetByTypeAsync(Guid companyId, RequestType requestType, CancellationToken cancellationToken = default);
    Task<List<RequestDefinition>> GetByCompanyAsync(Guid companyId, RequestType? type = null, CancellationToken cancellationToken = default);
    Task<bool> AnyDefinitionUsingRoleAsync(Guid companyId, UserRole role, CancellationToken ct = default);
}
