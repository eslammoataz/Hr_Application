using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class RequestDefinitionRepository : Repository<RequestDefinition>, IRequestDefinitionRepository
{
    public RequestDefinitionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<RequestDefinition?> GetByTypeAsync(Guid companyId, RequestType requestType, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(x => x.WorkflowSteps.OrderBy(s => s.SortOrder))
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.RequestType == requestType, cancellationToken);
    }

    public async Task<List<RequestDefinition>> GetByCompanyAsync(Guid companyId, RequestType? type = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(x => x.WorkflowSteps.OrderBy(s => s.SortOrder))
            .Where(x => x.CompanyId == companyId);

        if (type.HasValue)
            query = query.Where(x => x.RequestType == type.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<bool> AnyDefinitionUsingRoleAsync(Guid companyId, UserRole role, CancellationToken ct = default)
    {
        return await _dbSet
            .AnyAsync(x => x.CompanyId == companyId && x.WorkflowSteps.Any(s => s.RequiredRole == role), ct);
    }
}
