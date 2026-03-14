using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Repositories;

public interface ILeaveBalanceRepository : IRepository<LeaveBalance>
{
    Task<LeaveBalance?> GetAsync(Guid employeeId, LeaveType leaveType, int year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveBalance>> GetByEmployeeAsync(Guid employeeId, int year, CancellationToken cancellationToken = default);
}
