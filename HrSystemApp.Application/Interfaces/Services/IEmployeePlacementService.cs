using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Interfaces.Services;

public interface IEmployeePlacementService
{
    Task<Result<(Guid? DepartmentId, Guid? UnitId, Guid? TeamId)>> ResolvePlacementAsync(
        Guid companyId,
        Guid? departmentId,
        Guid? unitId,
        Guid? teamId,
        CancellationToken cancellationToken);

    Task<Result> AssignLeadershipIfNeededAsync(
        Employee employee,
        UserRole role,
        CancellationToken cancellationToken);
}
