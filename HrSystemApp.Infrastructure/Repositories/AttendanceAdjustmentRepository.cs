using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;

namespace HrSystemApp.Infrastructure.Repositories;

public class AttendanceAdjustmentRepository : Repository<AttendanceAdjustment>, IAttendanceAdjustmentRepository
{
    public AttendanceAdjustmentRepository(ApplicationDbContext context) : base(context)
    {
    }
}
