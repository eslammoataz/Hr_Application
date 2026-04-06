using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class AttendanceReminderLogRepository : Repository<AttendanceReminderLog>, IAttendanceReminderLogRepository
{
    public AttendanceReminderLogRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<bool> ExistsForWindowAsync(
        Guid attendanceId,
        AttendanceReminderType reminderType,
        string windowKey,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(
            x => x.AttendanceId == attendanceId &&
                 x.ReminderType == reminderType &&
                 x.WindowKey == windowKey,
            cancellationToken);
    }
}
