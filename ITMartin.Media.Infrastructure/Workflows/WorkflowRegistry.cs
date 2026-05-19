using ITMartin.Media.Application.Abstractions.Workflows;

namespace ITMartin.Media.Infrastructure.Workflows;

public sealed class WorkflowRegistry(
    IEnumerable<IWorkflowDefinition> workflows)
    : IWorkflowRegistry
{
    private readonly Dictionary<string, IWorkflowDefinition>
        _workflows =
            workflows.ToDictionary(
                x => x.Name,
                StringComparer.OrdinalIgnoreCase);

    public IWorkflowDefinition Resolve(
        string workflowName)
    {
        if (_workflows.TryGetValue(
                workflowName,
                out var workflow))
        {
            return workflow;
        }

        throw new InvalidOperationException(
            $"Workflow '{workflowName}' was not registered.");
    }
}