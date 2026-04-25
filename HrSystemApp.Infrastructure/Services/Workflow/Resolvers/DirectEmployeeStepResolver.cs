using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Infrastructure.Services.Workflow.Resolvers;

public sealed class DirectEmployeeStepResolver : WorkflowStepResolverBase
{
    private readonly Func<Guid, CancellationToken, Task<Employee?>> _getEmployeeById;
    private readonly ILogger _logger;
    private readonly LoggingOptions _loggingOptions;
    private readonly string _logAction;

    public DirectEmployeeStepResolver(
        Func<Guid, CancellationToken, Task<Employee?>> getEmployeeById,
        ILogger logger,
        LoggingOptions loggingOptions,
        string logAction = LogAction.Workflow.CreateRequest)
    {
        _getEmployeeById = getEmployeeById;
        _logger = logger;
        _loggingOptions = loggingOptions;
        _logAction = logAction;
    }

    public override WorkflowStepType Type => WorkflowStepType.DirectEmployee;

    public override async Task<Result<List<PlannedStepDto>>> ResolveAsync(
        WorkflowStepDto step,
        WorkflowResolutionContext context,
        WorkflowResolutionState state,
        CancellationToken ct)
    {
        _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
            "DirectEmployeeResolver_Start", new { DirectEmployeeId = step.DirectEmployeeId, RequesterId = context.RequesterEmployeeId });

        if (step.DirectEmployeeId == null)
        {
            _logger.LogWarning("[DirectEmployeeStepResolver] DirectEmployeeId is null");
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingDirectEmployeeId);
        }

        Employee? employee = null;
        if (context.EmployeesById.TryGetValue(step.DirectEmployeeId.Value, out var emp))
        {
            employee = emp;
        }
        else
        {
            employee = await _getEmployeeById(step.DirectEmployeeId.Value, ct);
        }

        if (employee == null)
        {
            _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                "DirectEmployeeResolver_NotFound", new { DirectEmployeeId = step.DirectEmployeeId });
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.DirectEmployeeNotInCompany);
        }

        _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
            "DirectEmployeeResolver_EmployeeFound", new { EmployeeId = employee.Id, Name = employee.FullName, Status = employee.EmploymentStatus.ToString() });

        if (state.SeenApproverIds.Contains(employee.Id))
        {
            _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                "DirectEmployeeResolver_Duplicate", new { EmployeeId = employee.Id });
            return Result.Success(new List<PlannedStepDto>());
        }

        var approvers = FilterApprovers(new[] { employee }, context.RequesterEmployeeId, state.SeenApproverIds);

        if (approvers.Count == 0)
        {
            _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                "DirectEmployeeResolver_NoApprovers", new { EmployeeId = employee.Id, RequesterId = context.RequesterEmployeeId });
            return Result.Success(new List<PlannedStepDto>());
        }

        var plannedStep = CreateStep(WorkflowStepType.DirectEmployee, employee.FullName, approvers);

        if (!state.TryAddStep(plannedStep))
        {
            _logger.LogDecision(_loggingOptions, _logAction, LogStage.Processing,
                "DirectEmployeeResolver_DuplicateStep", new { EmployeeId = employee.Id });
            return Result.Success(new List<PlannedStepDto>());
        }

        _logger.LogBusinessFlow(_loggingOptions, _logAction, LogStage.Processing,
            "DirectEmployeeResolver_Success", new { EmployeeId = employee.Id, ApproverCount = approvers.Count });

        return Result.Success(new List<PlannedStepDto> { plannedStep });
    }
}
