using HrSystemApp.Application.Interfaces.Repositories;
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
}
