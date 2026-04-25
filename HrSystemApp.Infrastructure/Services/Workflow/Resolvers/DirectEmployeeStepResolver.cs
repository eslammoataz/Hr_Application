using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Infrastructure.Services.Workflow.Resolvers;

public sealed class DirectEmployeeStepResolver : WorkflowStepResolverBase
{
    private readonly Func<Guid, CancellationToken, Task<Employee?>> _getEmployeeById;

    public DirectEmployeeStepResolver(Func<Guid, CancellationToken, Task<Employee?>> getEmployeeById)
    {
        _getEmployeeById = getEmployeeById;
    }

    public override WorkflowStepType Type => WorkflowStepType.DirectEmployee;

    public override async Task<Result<List<PlannedStepDto>>> ResolveAsync(
        WorkflowStepDto step,
        WorkflowResolutionContext context,
        WorkflowResolutionState state,
        CancellationToken ct)
    {
        if (step.DirectEmployeeId == null)
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.MissingDirectEmployeeId);

        Employee? employee = null;
        if (context.EmployeesById.TryGetValue(step.DirectEmployeeId.Value, out var emp))
            employee = emp;
        else
            employee = await _getEmployeeById(step.DirectEmployeeId.Value, ct);

        if (employee == null)
            return Result.Failure<List<PlannedStepDto>>(DomainErrors.Request.DirectEmployeeNotInCompany);

        if (state.SeenApproverIds.Contains(employee.Id))
            return Result.Success(new List<PlannedStepDto>());

        var approvers = FilterApprovers(new[] { employee }, context.RequesterEmployeeId, state.SeenApproverIds);
        if (approvers.Count == 0)
            return Result.Success(new List<PlannedStepDto>());

        var plannedStep = CreateStep(
            WorkflowStepType.DirectEmployee,
            employee.FullName,
            approvers);

        if (!state.TryAddStep(plannedStep))
            return Result.Success(new List<PlannedStepDto>());

        return Result.Success(new List<PlannedStepDto> { plannedStep });
    }
}
