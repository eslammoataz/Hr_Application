using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Infrastructure.Services.Workflow.Resolvers;

public sealed class WorkflowStepResolverFactory
{
    private readonly Dictionary<WorkflowStepType, IWorkflowStepResolver> _resolvers;

    public WorkflowStepResolverFactory(IEnumerable<IWorkflowStepResolver> resolvers)
    {
        _resolvers = resolvers.ToDictionary(r => r.Type);
    }

    public IWorkflowStepResolver Get(WorkflowStepType type)
    {
        return _resolvers.TryGetValue(type, out var resolver)
            ? resolver
            : throw new ArgumentException($"No resolver found for step type: {type}");
    }

    public bool HasResolver(WorkflowStepType type) => _resolvers.ContainsKey(type);
}
