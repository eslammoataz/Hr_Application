using HrSystemApp.Application.Interfaces.Repositories;

namespace HrSystemApp.Application.Interfaces;

/// <summary>
/// Unit of Work interface for transaction management
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IUserRepository Users { get; }
    IEmployeeRepository Employees { get; }
    ICompanyRepository Companies { get; }
    ICompanyLocationRepository CompanyLocations { get; }
    IDepartmentRepository Departments { get; }
    IUnitRepository Units { get; }
    ITeamRepository Teams { get; }
    ILeaveBalanceRepository LeaveBalances { get; }
    IContactAdminRequestRepository ContactAdminRequests { get; }
    IProfileUpdateRequestRepository ProfileUpdateRequests { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    IRequestDefinitionRepository RequestDefinitions { get; }
    IRequestRepository Requests { get; }
    ICompanyHierarchyPositionRepository HierarchyPositions { get; }
    IAttendanceRepository Attendances { get; }
    IAttendanceLogRepository AttendanceLogs { get; }
    IAttendanceReminderLogRepository AttendanceReminderLogs { get; }
    IAttendanceAdjustmentRepository AttendanceAdjustments { get; }
    IOrgNodeRepository OrgNodes { get; }
    IOrgNodeAssignmentRepository OrgNodeAssignments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
