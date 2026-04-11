using HrSystemApp.Application.Common;

namespace HrSystemApp.Application.DTOs.Employees;

public class EmployeesPagedResult : PagedResult<EmployeeResponse>
{
    public int TotalActive { get; init; }
    public int TotalInactive { get; init; }
}
