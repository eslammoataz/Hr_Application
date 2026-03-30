using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class RequestRepository : Repository<Request>, IRequestRepository
{
    public RequestRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Request?> GetByIdWithHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(x => x.Employee)
            .Include(x => x.ApprovalHistory)
                .ThenInclude(x => x.Approver)
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<List<Request>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Request>> GetPendingApprovalsAsync(Guid approverId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(x => x.CurrentApproverId == approverId && x.Status == RequestStatus.InProgress)
            .Include(x => x.Employee)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

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
}
