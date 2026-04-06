using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IAttendanceReminderLogRepository : IRepository<AttendanceReminderLog>
{
    Task<bool> ExistsForWindowAsync(
        Guid attendanceId,
        AttendanceReminderType reminderType,
        string windowKey,
        CancellationToken cancellationToken = default);
}
