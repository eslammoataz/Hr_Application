using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class RequestDefinitionRepository : Repository<RequestDefinition>, IRequestDefinitionRepository
{
    public RequestDefinitionRepository(ApplicationDbContext context) : base(context) { }

    // public async Task<RequestDefinition?> GetByTypeAsync(Guid companyId, Guid requestTypeId, CancellationToken cancellationToken = default)
    // {
    //     return await _dbSet
    //         .AsNoTracking()
    //         .Include(x => x.WorkflowSteps.OrderBy(s => s.SortOrder))
    //         .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.RequestTypeId == requestTypeId, cancellationToken);
    // }

    public async Task<RequestDefinition?> GetByTypeAsync(Guid companyId, Guid requestTypeId, CancellationToken cancellationToken = default)
    {
        var definition = await _dbSet
            .AsNoTracking()
            .Include(x => x.WorkflowSteps.OrderBy(s => s.SortOrder))
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.RequestTypeId == requestTypeId, cancellationToken);

        return definition;
    }

    public async Task<List<RequestDefinition>> GetByCompanyAsync(Guid companyId, Guid? requestTypeId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(x => x.WorkflowSteps.OrderBy(s => s.SortOrder))
            .Where(x => x.CompanyId == companyId);

        if (requestTypeId.HasValue)
            query = query.Where(x => x.RequestTypeId == requestTypeId.Value);

        return await query.ToListAsync(cancellationToken);
    }

    [Obsolete("Role-based workflow is deprecated. Use OrgNode-based workflow steps.")]
    public async Task<bool> AnyDefinitionUsingRoleAsync(Guid companyId, UserRole role, CancellationToken ct = default)
    {
        return await Task.FromResult(false);
    }

    public async Task<bool> IsRoleInUseAsync(Guid companyRoleId, CancellationToken ct = default)
    {
        return await _context.RequestDefinitions
            .AnyAsync(d => d.WorkflowSteps.Any(s => s.CompanyRoleId == companyRoleId), ct);
    }
}
