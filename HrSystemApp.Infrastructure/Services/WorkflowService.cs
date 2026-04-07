using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Infrastructure.Services;

public class WorkflowService : IWorkflowService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager, ILogger<WorkflowService> logger)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<List<Employee>> GetApprovalPathAsync(Guid employeeId, RequestType requestType, CancellationToken cancellationToken = default)
    {
        var employee = await _unitOfWork.Employees.GetByIdAsync(employeeId, cancellationToken);
        if (employee == null)
        {
            _logger.LogWarning("GetApprovalPathAsync failed: Employee with ID {EmployeeId} not found.", employeeId);
            return new List<Employee>();
        }

        _logger.LogInformation("Resolving approval path for Employee {EmployeeId} ({FullName}) and RequestType {RequestType}", 
            employeeId, employee.FullName, requestType);

        var definition = await _unitOfWork.RequestDefinitions.GetByTypeAsync(employee.CompanyId, requestType, cancellationToken);
        if (definition == null || !definition.IsActive)
        {
            _logger.LogWarning("No active definition found for request type {RequestType} in company {CompanyId}", requestType, employee.CompanyId);
            return new List<Employee>();
        }

        var approvalPath = new List<Employee>();

        foreach (var step in definition.WorkflowSteps.OrderBy(s => s.SortOrder))
        {
            var approver = await ResolveApproverAsync(employee, step.RequiredRole, cancellationToken);
            
            if (approver != null)
            {
                _logger.LogInformation("Step {SortOrder}: Role {Role} resolved to Employee {ApproverId} ({ApproverName})", 
                    step.SortOrder, step.RequiredRole, approver.Id, approver.FullName);

                if (approver.Id == employeeId)
                {
                    _logger.LogInformation("Skipping step {SortOrder} (Role: {Role}) for employee {EmployeeId} because they are the same person.", step.SortOrder, step.RequiredRole, employeeId);
                    continue;
                }

                if (!approvalPath.Any(a => a.Id == approver.Id))
                {
                    approvalPath.Add(approver);
                }
            }
        }

        if (approvalPath.Count == 0)
        {
            _logger.LogWarning("No approvers resolved from workflow stages. Attempting fail-safe resolution.");
            var failSafe = await GetFailSafeApproverAsync(employee.CompanyId, cancellationToken);
            if (failSafe != null && failSafe.Id != employeeId)
            {
                _logger.LogInformation("Fail-safe approver resolved: {ApproverId} ({ApproverName})", failSafe.Id, failSafe.FullName);
                approvalPath.Add(failSafe);
            }
        }
        
        _logger.LogInformation("Resolved final approval path with {Count} stages.", approvalPath.Count);

        return approvalPath;
    }

    private async Task<Employee?> ResolveApproverAsync(Employee requester, UserRole role, CancellationToken cancellationToken)
    {
        switch (role)
        {
            case UserRole.TeamLeader:
                if (requester.TeamId.HasValue)
                {
                    var team = await _unitOfWork.Teams.GetByIdAsync(requester.TeamId.Value, cancellationToken);
                    if (team?.TeamLeaderId.HasValue == true)
                        return await _unitOfWork.Employees.GetByIdAsync(team.TeamLeaderId.Value, cancellationToken);
                }
                break;

            case UserRole.UnitLeader:
                if (requester.UnitId.HasValue)
                {
                    var unit = await _unitOfWork.Units.GetByIdAsync(requester.UnitId.Value, cancellationToken);
                    if (unit?.UnitLeaderId.HasValue == true)
                        return await _unitOfWork.Employees.GetByIdAsync(unit.UnitLeaderId.Value, cancellationToken);
                }
                break;

            case UserRole.DepartmentManager:
                if (requester.DepartmentId.HasValue)
                {
                    var dept = await _unitOfWork.Departments.GetByIdAsync(requester.DepartmentId.Value, cancellationToken);
                    if (dept?.ManagerId.HasValue == true)
                        return await _unitOfWork.Employees.GetByIdAsync(dept.ManagerId.Value, cancellationToken);
                }
                break;

            case UserRole.VicePresident:
                if (requester.DepartmentId.HasValue)
                {
                    var dept = await _unitOfWork.Departments.GetByIdAsync(requester.DepartmentId.Value, cancellationToken);
                    if (dept?.VicePresidentId.HasValue == true)
                        return await _unitOfWork.Employees.GetByIdAsync(dept.VicePresidentId.Value, cancellationToken);
                }
                break;

            case UserRole.CEO:
            case UserRole.HR:
            case UserRole.AssetAdmin:
            case UserRole.CompanyAdmin:
                return await FindFirstEmployeeInRoleAsync(requester.CompanyId, role.ToString(), cancellationToken);

            default:
                break;
        }

        return null;
    }

    private async Task<Employee?> FindFirstEmployeeInRoleAsync(Guid companyId, string roleName, CancellationToken cancellationToken)
    {
        var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
        var userIds = usersInRole.Select(u => u.Id).ToList();

        // Find the first employee in this company that belongs to one of these users
        var employees = await _unitOfWork.Employees.FindAsync(e => 
            e.CompanyId == companyId && !e.IsDeleted && e.UserId != null && userIds.Contains(e.UserId), cancellationToken);

        return employees.FirstOrDefault();
    }

    private async Task<Employee?> GetFailSafeApproverAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var approver = await FindFirstEmployeeInRoleAsync(companyId, UserRole.CompanyAdmin.ToString(), cancellationToken);
        return approver ?? await FindFirstEmployeeInRoleAsync(companyId, UserRole.HR.ToString(), cancellationToken);
    }
}
