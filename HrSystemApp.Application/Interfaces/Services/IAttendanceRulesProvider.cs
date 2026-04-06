using HrSystemApp.Application.DTOs.Attendance;

namespace HrSystemApp.Application.Interfaces.Services;

public interface IAttendanceRulesProvider
{
    Task<ShiftRulesUtc> GetRulesAsync(Guid employeeId, DateOnly businessDate, CancellationToken cancellationToken = default);
    Task<DateOnly> ResolveBusinessDateAsync(Guid employeeId, DateTime timestampUtc, CancellationToken cancellationToken = default);
}
