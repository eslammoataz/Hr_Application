using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class AttendanceLogRepository : Repository<AttendanceLog>, IAttendanceLogRepository
{
    public AttendanceLogRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<AttendanceLog?> GetLastClockInAsync(Guid attendanceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(x => x.AttendanceId == attendanceId && x.Type == AttendanceLogType.ClockIn)
            .OrderByDescending(x => x.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceLog>> GetByAttendanceIdAsync(Guid attendanceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(x => x.AttendanceId == attendanceId)
            .OrderBy(x => x.TimestampUtc)
            .ToListAsync(cancellationToken);
    }
}
