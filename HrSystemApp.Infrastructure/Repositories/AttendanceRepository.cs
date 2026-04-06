using HrSystemApp.Application.Common;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class AttendanceRepository : Repository<Attendance>, IAttendanceRepository
{
    public AttendanceRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Attendance?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(x => x.FirstClockInLog)
            .Include(x => x.LastClockOutLog)
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Date == date, cancellationToken);
    }

    public async Task<Attendance?> GetOpenAttendanceAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(x => x.FirstClockInLog)
            .Include(x => x.LastClockOutLog)
            .OrderByDescending(x => x.Date)
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.FirstClockInUtc != null && x.LastClockOutUtc == null, cancellationToken);
    }

    public async Task<IReadOnlyList<Attendance>> GetIncompleteAttendancesAsync(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(x => x.FirstClockInUtc != null && x.LastClockOutUtc == null && x.FirstClockInUtc <= dueBeforeUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Attendance>> GetMyAttendancePagedAsync(
        Guid employeeId,
        DateOnly fromDate,
        DateOnly toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.Date >= fromDate && x.Date <= toDate);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.Date)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<Attendance>.Create(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PagedResult<Attendance>> GetCompanyAttendancePagedAsync(
        Guid companyId,
        DateOnly fromDate,
        DateOnly toDate,
        Guid? employeeId,
        AttendanceStatus? status,
        bool? isLate,
        bool? isEarlyLeave,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking()
            .Include(x => x.Employee)
            .Where(x => x.Employee.CompanyId == companyId && x.Date >= fromDate && x.Date <= toDate);

        if (employeeId.HasValue)
        {
            query = query.Where(x => x.EmployeeId == employeeId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (isLate.HasValue)
        {
            query = query.Where(x => x.IsLate == isLate.Value);
        }

        if (isEarlyLeave.HasValue)
        {
            query = query.Where(x => x.IsEarlyLeave == isEarlyLeave.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.Date)
            .ThenBy(x => x.Employee.FullName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<Attendance>.Create(items, pageNumber, pageSize, totalCount);
    }
}
