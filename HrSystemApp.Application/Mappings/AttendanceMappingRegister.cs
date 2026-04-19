using HrSystemApp.Application.DTOs.Attendance;
using HrSystemApp.Domain.Models;
using Mapster;

namespace HrSystemApp.Application.Mappings;

public class AttendanceMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Attendance, AttendanceSummaryResponse>()
            .Map(dest => dest.EmployeeId,       src => src.EmployeeId)
            .Map(dest => dest.EmployeeName,     src => src.Employee != null ? src.Employee.FullName : string.Empty)
            .Map(dest => dest.Date,             src => src.Date)
            .Map(dest => dest.FirstClockInUtc,  src => src.FirstClockInUtc)
            .Map(dest => dest.LastClockOutUtc,  src => src.LastClockOutUtc)
            .Map(dest => dest.TotalHours,       src => src.TotalHours)
            .Map(dest => dest.Status,           src => src.Status.ToString())
            .Map(dest => dest.IsLate,           src => src.IsLate)
            .Map(dest => dest.IsEarlyLeave,     src => src.IsEarlyLeave)
            .Map(dest => dest.Reason,           src => src.Reason)
            // The company-wide list is kept lean — sessions are fetched on demand
            // via GET /attendance/{id}/sessions to avoid loading thousands of log rows.
            .Map(dest => dest.Sessions,         src => new List<AttendanceSessionDto>());
    }
}
