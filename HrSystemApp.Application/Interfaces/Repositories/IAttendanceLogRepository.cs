using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IAttendanceLogRepository : IRepository<AttendanceLog>
{
    Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent ClockIn log for the given attendance (i.e. the start of the current open session).
    /// </summary>
    Task<AttendanceLog?> GetLastClockInAsync(Guid attendanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all logs for the given attendance ordered by TimestampUtc ascending.
    /// Used for session pairing, TotalHours recalculation, and the sessions endpoint.
    /// </summary>
    Task<IReadOnlyList<AttendanceLog>> GetByAttendanceIdAsync(Guid attendanceId, CancellationToken cancellationToken = default);
}
