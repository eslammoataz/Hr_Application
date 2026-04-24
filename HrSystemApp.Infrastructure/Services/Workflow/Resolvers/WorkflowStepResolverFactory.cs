using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Infrastructure.Services.Workflow.Resolvers;

public sealed class WorkflowStepResolverFactory
{
    private readonly Dictionary<WorkflowStepType, IWorkflowStepResolver> _resolvers;

    public WorkflowStepResolverFactory(IEnumerable<IWorkflowStepResolver> resolvers)
    {
        _resolvers = resolvers.ToDictionary(r => r.Type);
    }

    public Result<IWorkflowStepResolver> Get(WorkflowStepType type)
    {
        return _resolvers.TryGetValue(type, out var resolver)
            ? Result.Success(resolver)
            : Result.Failure<IWorkflowStepResolver>(DomainErrors.Workflows.InvalidStep);
    }
}
