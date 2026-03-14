using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class LeaveBalanceRepository : Repository<LeaveBalance>, ILeaveBalanceRepository
{
    public LeaveBalanceRepository(ApplicationDbContext context) : base(context) { }

    public async Task<LeaveBalance?> GetAsync(Guid employeeId, LeaveType leaveType, int year, CancellationToken cancellationToken = default)
        => await _dbSet.FirstOrDefaultAsync(
            lb => lb.EmployeeId == employeeId && lb.LeaveType == leaveType && lb.Year == year,
            cancellationToken);

    public async Task<IReadOnlyList<LeaveBalance>> GetByEmployeeAsync(Guid employeeId, int year, CancellationToken cancellationToken = default)
        => await _dbSet
            .Where(lb => lb.EmployeeId == employeeId && lb.Year == year)
            .ToListAsync(cancellationToken);
}
