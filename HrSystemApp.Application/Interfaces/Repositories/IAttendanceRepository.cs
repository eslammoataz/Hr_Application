using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface IAttendanceRepository : IRepository<Attendance>
{
    Task<Attendance?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken = default);
    Task<Attendance?> GetOpenAttendanceAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Attendance>> GetIncompleteAttendancesAsync(DateTime dueBeforeUtc, CancellationToken cancellationToken = default);
    Task<PagedResult<Attendance>> GetMyAttendancePagedAsync(
        Guid employeeId,
        DateOnly fromDate,
        DateOnly toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Attendance>> GetCompanyAttendancePagedAsync(
        Guid companyId,
        DateOnly fromDate,
        DateOnly toDate,
        Guid? employeeId,
        AttendanceStatus? status,
        bool? isLate,
        bool? isEarlyLeave,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
