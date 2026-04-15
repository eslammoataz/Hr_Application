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
            .AsNoTracking()
            .Include(x => x.WorkflowSteps.OrderBy(s => s.SortOrder))
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.RequestType == requestType, cancellationToken);
    }

    public async Task<List<RequestDefinition>> GetByCompanyAsync(Guid companyId, RequestType? type = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(x => x.WorkflowSteps.OrderBy(s => s.SortOrder))
            .Where(x => x.CompanyId == companyId);

        if (type.HasValue)
            query = query.Where(x => x.RequestType == type.Value);

        return await query.ToListAsync(cancellationToken);
    }

    [Obsolete("Role-based workflow is deprecated. Use OrgNode-based workflow steps.")]
    public async Task<bool> AnyDefinitionUsingRoleAsync(Guid companyId, UserRole role, CancellationToken ct = default)
    {
        // This method is deprecated and always returns false since we no longer use role-based workflow.
        return await Task.FromResult(false);
    }
}
