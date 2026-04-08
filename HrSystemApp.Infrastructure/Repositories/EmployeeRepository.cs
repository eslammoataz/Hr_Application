using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class EmployeeRepository : Repository<Employee>, IEmployeeRepository
{
    public EmployeeRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Employee?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        => await _dbSet.FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

    public async Task<Employee?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbSet
            .Include(e => e.Department)
            .Include(e => e.Unit)
            .Include(e => e.Team)
            .Include(e => e.Manager)
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, cancellationToken);

    public async Task<EmployeeProfileDto?> GetProfileByUserIdAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.AsQueryable()
            .Where(e => e.UserId == userId && !e.IsDeleted)
            .Select(e => new EmployeeProfileDto
            {
                Id = e.Id,
                CompanyId = e.CompanyId,
                EmployeeCode = e.EmployeeCode,
                FullName = e.FullName,
                Email = e.Email,
                PhoneNumber = e.PhoneNumber,
                Address = e.Address,
                DepartmentName = e.Department != null ? e.Department.Name : string.Empty,
                UnitName = e.Unit != null ? e.Unit.Name : string.Empty,
                TeamName = e.Team != null ? e.Team.Name : string.Empty,
                ManagerName = e.Manager != null ? e.Manager.FullName : string.Empty,
                EmploymentStatus = e.EmploymentStatus.ToString(),
                MedicalClass = e.MedicalClass.HasValue ? e.MedicalClass.Value.ToString() : null,
                CompanyLocationName = e.CompanyLocation != null ? e.CompanyLocation.LocationName : string.Empty,
                ContractEndDate = e.ContractEndDate
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

// for me "needs some query optimization"  
    public async Task<PagedResult<Employee>> GetPagedAsync(
        Guid? companyId, Guid? teamId, string? searchTerm,
        int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(e => e.Department)
            .Include(e => e.Unit)
            .Include(e => e.Team)
            .Include(e => e.Manager)
            .Include(e => e.User)
            .Where(e => !e.IsDeleted)
            .AsQueryable();

        if (companyId.HasValue)
            query = query.Where(e => e.CompanyId == companyId.Value);

        if (teamId.HasValue)
            query = query.Where(e => e.TeamId == teamId.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(e =>
                e.FullName.ToLower().Contains(term) ||
                e.Email.ToLower().Contains(term) ||
                e.EmployeeCode.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(e => e.FullName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<Employee>.Create(items, pageNumber, pageSize, totalCount);
    }
}
