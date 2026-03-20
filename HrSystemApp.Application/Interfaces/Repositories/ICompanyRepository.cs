using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface ICompanyRepository : IRepository<Company>
{
    Task<Company?> GetWithLocationsAsync(Guid id, CancellationToken cancellationToken = default);
}
