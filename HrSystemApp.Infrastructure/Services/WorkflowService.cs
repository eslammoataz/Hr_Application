using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Infrastructure.Services;

[Obsolete("Role-based workflow is deprecated. Use IWorkflowResolutionService for OrgNode-based workflow.")]
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
        // Deprecated: This method uses role-based workflow which is no longer supported.
        // Use IWorkflowResolutionService.BuildApprovalChainAsync instead.
        _logger.LogWarning("GetApprovalPathAsync is deprecated. Use IWorkflowResolutionService instead.");
        return new List<Employee>();
    }
}
