using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees.ProfileUpdateRequests;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class ProfileUpdateRequestRepository : Repository<ProfileUpdateRequest>, IProfileUpdateRequestRepository
{
    public ProfileUpdateRequestRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<ProfileUpdateRequestDto>> GetPagedRequestsByCompanyAsync(Guid companyId, ProfileUpdateRequestStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.ProfileUpdateRequests
            .AsNoTracking()
            .Where(r => r.Employee.CompanyId == companyId);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ProfileUpdateRequestDto
            {
                Id = r.Id,
                EmployeeId = r.EmployeeId,
                EmployeeName = r.Employee.FullName,
                ChangesJson = r.ChangesJson,
                Status = r.Status,
                EmployeeComment = r.EmployeeComment,
                HrNote = r.HrNote,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return PagedResult<ProfileUpdateRequestDto>.Create(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PagedResult<ProfileUpdateRequestDto>> GetPagedMyRequestsAsync(Guid employeeId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.ProfileUpdateRequests
            .AsNoTracking()
            .Where(r => r.EmployeeId == employeeId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ProfileUpdateRequestDto
            {
                Id = r.Id,
                EmployeeId = r.EmployeeId,
                EmployeeName = r.Employee.FullName,
                ChangesJson = r.ChangesJson,
                Status = r.Status,
                EmployeeComment = r.EmployeeComment,
                HrNote = r.HrNote,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return PagedResult<ProfileUpdateRequestDto>.Create(items, pageNumber, pageSize, totalCount);
    }
}
