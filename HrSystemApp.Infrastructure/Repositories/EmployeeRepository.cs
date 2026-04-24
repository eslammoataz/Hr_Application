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
            .Include(e => e.Manager)
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<EmployeeProfileDto?> GetProfileByUserIdAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.AsQueryable()
            .Where(e => e.UserId == userId)
            .Select(e => new EmployeeProfileDto
            {
                Id = e.Id,
                CompanyId = e.CompanyId,
                EmployeeCode = e.EmployeeCode,
                FullName = e.FullName,
                Email = e.Email,
                PhoneNumber = e.PhoneNumber,
                Address = e.Address,
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
            .Where(e => e.CompanyId == companyId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Builds a paged list of employees applying optional filters and returns the page with aggregate counts.
    /// </summary>
    /// <param name="companyId">If provided, limits results to employees belonging to the specified company.</param>
    /// <param name="searchTerm">If provided and not empty, filters employees whose FullName, Email, or EmployeeCode match the term (case-insensitive, partial match).</param>
    /// <param name="role">If provided, filters employees by their assigned role name.</param>
    /// <param name="employmentStatus">If provided, filters employees by the given employment status.</param>
    /// <param name="pageNumber">One-based page number to return.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <returns>
    /// An EmployeesPagedResult containing:
    /// - Items: the paged list of EmployeeResponse items;
    /// - PageNumber and PageSize: the requested paging parameters;
    /// - TotalCount: total number of matching employees;
    /// - TotalActive: count of employees in active-like statuses (Active, Probation, OnLeave);
    /// - TotalInactive: count of employees in inactive-like statuses (Inactive, Suspended, Terminated).
    /// </returns>
    public async Task<EmployeesPagedResult> GetPagedForListAsync(
        Guid? companyId,
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
            select new EmployeeListRow
            {
                Id = employee.Id,
                FullName = employee.FullName,
                Email = employee.Email,
                PhoneNumber = employee.PhoneNumber,
                Address = employee.Address,
                EmployeeCode = employee.EmployeeCode,
                CompanyId = employee.CompanyId,
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

    /// <summary>
            /// Fetches Employee entities for the specified identifiers.
            /// </summary>
            /// <param name="ids">Sequence of employee <see cref="Guid"/> identifiers to retrieve; only matching employees are included in the result.</param>
            /// <param name="ct">Token to observe while waiting for the database operation to complete.</param>
            /// <returns>A dictionary that maps each found employee Id to its corresponding <see cref="Employee"/> entity.</returns>
            public async Task<Dictionary<Guid, Employee>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        => await _dbSet.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

    private sealed class EmployeeListRow
    {
        public Guid Id { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string PhoneNumber { get; init; } = string.Empty;
        public string? Address { get; init; }
        public string EmployeeCode { get; init; } = string.Empty;
        public Guid CompanyId { get; init; }
        public Guid? ManagerId { get; init; }
        public string? ManagerName { get; init; }
        public EmploymentStatus EmploymentStatus { get; init; }
        public string Role { get; init; } = string.Empty;
        public MedicalClass? MedicalClass { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
