using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IRequestDefinitionRepository : IRepository<RequestDefinition>
{
    Task<RequestDefinition?> GetByTypeAsync(Guid companyId, RequestType requestType, CancellationToken cancellationToken = default);
    Task<List<RequestDefinition>> GetByCompanyAsync(Guid companyId, RequestType? type = null, CancellationToken cancellationToken = default);
}

public interface IRequestRepository : IRepository<Request>
{
    Task<Request?> GetByIdWithHistoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Request>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<List<Request>> GetPendingApprovalsAsync(Guid approverId, CancellationToken cancellationToken = default);
}
