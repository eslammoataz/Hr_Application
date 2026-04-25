using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IRequestTypeRepository
{
    Task<RequestType?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<RequestType?> GetByKeyNameAsync(string keyName, Guid? companyId = null, CancellationToken ct = default);
    Task<IReadOnlyList<RequestType>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<RequestType> AddAsync(RequestType requestType, CancellationToken ct = default);
    Task UpdateAsync(RequestType requestType, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
