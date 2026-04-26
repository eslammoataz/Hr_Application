using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Storage;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;

namespace HrSystemApp.Infrastructure.Repositories;

/// <summary>
/// Unit of Work implementation
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private IDbContextTransaction? _transaction;

    private IUserRepository? _userRepository;
    private IEmployeeRepository? _employeeRepository;
    private ICompanyRepository? _companyRepository;
    private ICompanyLocationRepository? _companyLocationRepository;
    private ILeaveBalanceRepository? _leaveBalanceRepository;
    private IContactAdminRequestRepository? _contactAdminRequestRepository;
    private IProfileUpdateRequestRepository? _profileUpdateRequestRepository;
    private IRefreshTokenRepository? _refreshTokenRepository;
    private IRequestDefinitionRepository? _requestDefinitionRepository;
    private IRequestRepository? _requestRepository;
    private IRequestTypeRepository? _requestTypeRepository;
    private ICompanyHierarchyPositionRepository? _hierarchyPositionRepository;
    private IAttendanceRepository? _attendanceRepository;
    private IAttendanceLogRepository? _attendanceLogRepository;
    private IAttendanceReminderLogRepository? _attendanceReminderLogRepository;
    private IAttendanceAdjustmentRepository? _attendanceAdjustmentRepository;
    private IOrgNodeRepository? _orgNodeRepository;
    private IOrgNodeAssignmentRepository? _orgNodeAssignmentRepository;
    private ICompanyRoleRepository? _companyRoleRepository;
    private IEmployeeCompanyRoleRepository? _employeeCompanyRoleRepository;


    public UnitOfWork(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public IUserRepository Users =>
        _userRepository ??= new UserRepository(_context, _userManager);

    public IEmployeeRepository Employees =>
        _employeeRepository ??= new EmployeeRepository(_context);

    public ICompanyRepository Companies =>
        _companyRepository ??= new CompanyRepository(_context);

    public ICompanyLocationRepository CompanyLocations =>
        _companyLocationRepository ??= new CompanyLocationRepository(_context);

    public ILeaveBalanceRepository LeaveBalances =>
        _leaveBalanceRepository ??= new LeaveBalanceRepository(_context);

    public IContactAdminRequestRepository ContactAdminRequests =>
        _contactAdminRequestRepository ??= new ContactAdminRequestRepository(_context);

    public IProfileUpdateRequestRepository ProfileUpdateRequests =>
        _profileUpdateRequestRepository ??= new ProfileUpdateRequestRepository(_context);

    public IRefreshTokenRepository RefreshTokens =>
        _refreshTokenRepository ??= new RefreshTokenRepository(_context);

    public IRequestDefinitionRepository RequestDefinitions =>
        _requestDefinitionRepository ??= new RequestDefinitionRepository(_context);

    public IRequestRepository Requests =>
        _requestRepository ??= new RequestRepository(_context);

    public IRequestTypeRepository RequestTypes =>
        _requestTypeRepository ??= new RequestTypeRepository(_context);

    public ICompanyHierarchyPositionRepository HierarchyPositions =>
        _hierarchyPositionRepository ??= new CompanyHierarchyPositionRepository(_context);

    public IAttendanceRepository Attendances =>
        _attendanceRepository ??= new AttendanceRepository(_context);

    public IAttendanceLogRepository AttendanceLogs =>
        _attendanceLogRepository ??= new AttendanceLogRepository(_context);

    public IAttendanceReminderLogRepository AttendanceReminderLogs =>
        _attendanceReminderLogRepository ??= new AttendanceReminderLogRepository(_context);

    public IAttendanceAdjustmentRepository AttendanceAdjustments =>
        _attendanceAdjustmentRepository ??= new AttendanceAdjustmentRepository(_context);

    public IOrgNodeRepository OrgNodes =>
        _orgNodeRepository ??= new OrgNodeRepository(_context);

    public IOrgNodeAssignmentRepository OrgNodeAssignments =>
        _orgNodeAssignmentRepository ??= new OrgNodeAssignmentRepository(_context);

    public ICompanyRoleRepository CompanyRoles =>
        _companyRoleRepository ??= new CompanyRoleRepository(_context);

    public IEmployeeCompanyRoleRepository EmployeeCompanyRoles =>
        _employeeCompanyRoleRepository ??= new EmployeeCompanyRoleRepository(_context);


    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("A transaction is already in progress.");

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No transaction in progress.");

        try
        {
            await _transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No transaction in progress.");

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        await _context.DisposeAsync();
    }
}
