using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
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

    public async Task<IReadOnlyList<Employee>> GetByCompanyAsync(Guid companyId,
        CancellationToken cancellationToken = default)
        => await _dbSet
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && !e.IsDeleted)
            .ToListAsync(cancellationToken);

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

    public async Task<EmployeesPagedResult> GetPagedForListAsync(
        Guid? companyId,
        Guid? teamId,
        string? searchTerm,
        UserRole? role,
        EmploymentStatus? employmentStatus,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // ──────────────────────────────────────────────────────────
        // 1. Main query with a correlated subquery for role name
        // ──────────────────────────────────────────────────────────
        var userRoles = _context.UserRoles.AsNoTracking();
        var roles = _context.Roles.AsNoTracking();

        var query =
            from employee in _dbSet.AsNoTracking()
            where !employee.IsDeleted
            select new EmployeeListRow
            {
                Id = employee.Id,
                FullName = employee.FullName,
                Email = employee.Email,
                PhoneNumber = employee.PhoneNumber,
                Address = employee.Address,
                EmployeeCode = employee.EmployeeCode,
                CompanyId = employee.CompanyId,
                DepartmentId = employee.DepartmentId,
                DepartmentName = employee.Department != null ? employee.Department.Name : null,
                UnitId = employee.UnitId,
                UnitName = employee.Unit != null ? employee.Unit.Name : null,
                TeamId = employee.TeamId,
                TeamName = employee.Team != null ? employee.Team.Name : null,
                ManagerId = employee.ManagerId,
                ManagerName = employee.Manager != null ? employee.Manager.FullName : null,
                EmploymentStatus = employee.EmploymentStatus,
                Role = (
                    from ur in userRoles
                    join r in roles on ur.RoleId equals r.Id
                    where ur.UserId == employee.UserId
                    select r.Name
                ).FirstOrDefault() ?? string.Empty,
                MedicalClass = employee.MedicalClass,
                CreatedAt = employee.CreatedAt
            };

        // ──────────────────────────────────────────────────────────
        // 2. Apply filters
        // ──────────────────────────────────────────────────────────
        if (companyId.HasValue)
            query = query.Where(e => e.CompanyId == companyId.Value);

        if (teamId.HasValue)
            query = query.Where(e => e.TeamId == teamId.Value);

        if (role.HasValue)
        {
            var roleName = role.Value.ToString();
            query = query.Where(e => e.Role == roleName);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm.Trim()}%";
            query = query.Where(e =>
                EF.Functions.ILike(e.FullName, pattern) ||
                EF.Functions.ILike(e.Email, pattern) ||
                EF.Functions.ILike(e.EmployeeCode, pattern));
        }

        if (employmentStatus.HasValue)
            query = query.Where(e => e.EmploymentStatus == employmentStatus.Value);

        // ──────────────────────────────────────────────────────────
        // 3. Aggregates — single round-trip, after ALL filters
        // ──────────────────────────────────────────────────────────
        var aggregates = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                // Active: Active, Probation, OnLeave
                TotalActive = g.Count(e => e.EmploymentStatus == EmploymentStatus.Active || 
                                           e.EmploymentStatus == EmploymentStatus.Probation || 
                                           e.EmploymentStatus == EmploymentStatus.OnLeave),
                // Inactive: Inactive, Suspended, Terminated
                TotalInactive = g.Count(e => e.EmploymentStatus == EmploymentStatus.Inactive || 
                                             e.EmploymentStatus == EmploymentStatus.Suspended || 
                                             e.EmploymentStatus == EmploymentStatus.Terminated),
                TotalCount = g.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        // ──────────────────────────────────────────────────────────
        // 4. Paginate
        // ──────────────────────────────────────────────────────────
        var rows = await query
            .OrderBy(e => e.FullName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // ──────────────────────────────────────────────────────────
        // 5. Map to response
        // ──────────────────────────────────────────────────────────
        var items = rows.Select(row => new EmployeeResponse
        {
            Id = row.Id,
            FullName = row.FullName,
            Email = row.Email,
            PhoneNumber = row.PhoneNumber,
            Address = row.Address,
            EmployeeCode = row.EmployeeCode,
            CompanyId = row.CompanyId,
            DepartmentId = row.DepartmentId,
            DepartmentName = row.DepartmentName,
            UnitId = row.UnitId,
            UnitName = row.UnitName,
            TeamId = row.TeamId,
            TeamName = row.TeamName,
            ManagerId = row.ManagerId,
            ManagerName = row.ManagerName,
            EmploymentStatus = row.EmploymentStatus.ToString(),
            Role = row.Role,
            MedicalClass = row.MedicalClass?.ToString(),
            CreatedAt = row.CreatedAt
        }).ToList();

        return new EmployeesPagedResult
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = aggregates?.TotalCount ?? 0,
            TotalActive = aggregates?.TotalActive ?? 0,
            TotalInactive = aggregates?.TotalInactive ?? 0
        };
    }

    private sealed class EmployeeListRow
    {
        public Guid Id { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string PhoneNumber { get; init; } = string.Empty;
        public string? Address { get; init; }
        public string EmployeeCode { get; init; } = string.Empty;
        public Guid CompanyId { get; init; }
        public Guid? DepartmentId { get; init; }
        public string? DepartmentName { get; init; }
        public Guid? UnitId { get; init; }
        public string? UnitName { get; init; }
        public Guid? TeamId { get; init; }
        public string? TeamName { get; init; }
        public Guid? ManagerId { get; init; }
        public string? ManagerName { get; init; }
        public EmploymentStatus EmploymentStatus { get; init; }
        public string Role { get; init; } = string.Empty;
        public MedicalClass? MedicalClass { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
