using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Infrastructure.Services.Workflow.Resolvers;

public interface IWorkflowStepResolver
{
    WorkflowStepType Type { get; }
    Task<Result<List<PlannedStepDto>>> ResolveAsync(
        WorkflowStepDto step,
        WorkflowResolutionContext context,
        WorkflowResolutionState state,
        CancellationToken ct);
}
