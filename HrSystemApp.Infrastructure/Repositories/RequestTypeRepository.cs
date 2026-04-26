using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class RequestTypeRepository : IRequestTypeRepository
{
    private readonly ApplicationDbContext _context;

    public RequestTypeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RequestType?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.RequestTypes
            .FirstOrDefaultAsync(rt => rt.Id == id && !rt.IsDeleted, ct);
    }

    public async Task<RequestType?> GetByKeyNameAsync(string keyName, Guid? companyId = null, CancellationToken ct = default)
    {
        var query = _context.RequestTypes.Where(rt => rt.KeyName == keyName && !rt.IsDeleted);

        if (companyId.HasValue)
        {
            // First try to find company-specific custom type
            var customType = await query.FirstOrDefaultAsync(rt => rt.CompanyId == companyId.Value, ct);
            if (customType != null)
                return customType;
        }

        // Fall back to system type (CompanyId is null)
        return await query.FirstOrDefaultAsync(rt => rt.CompanyId == null, ct);
    }

    public async Task<IReadOnlyList<RequestType>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        // Return system types (IsSystemType = true) and custom types for this company
        return await _context.RequestTypes
            .Where(rt => !rt.IsDeleted && (rt.IsSystemType || rt.CompanyId == companyId))
            .OrderBy(rt => rt.DisplayName)
            .ToListAsync(ct);
    }

    public async Task<RequestType> AddAsync(RequestType requestType, CancellationToken ct = default)
    {
        await _context.RequestTypes.AddAsync(requestType, ct);
        return requestType;
    }

    public async Task UpdateAsync(RequestType requestType, CancellationToken ct = default)
    {
        _context.RequestTypes.Update(requestType);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var requestType = await GetByIdAsync(id, ct);
        if (requestType != null)
        {
            requestType.IsDeleted = true;
            _context.RequestTypes.Update(requestType);
        }
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await _context.RequestTypes.CountAsync(rt => !rt.IsDeleted, ct);
    }
}
