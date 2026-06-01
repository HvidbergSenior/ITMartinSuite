using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Runtime.Registry;

public sealed class WorkflowRegistry(
    IEnumerable<IWorkflowDefinition> workflows)
    : IWorkflowRegistry
{
    private readonly Dictionary<WorkflowType, IWorkflowDefinition>
        _workflows =
            workflows.ToDictionary(
                x => x.WorkflowType);

    public IWorkflowDefinition Resolve(
        WorkflowType workflowType)
    {
        if (_workflows.TryGetValue(
                workflowType,
                out var workflow))
        {
            return workflow;
        }

        throw new InvalidOperationException(
            $"Workflow '{workflowType}' was not registered.");
    }
}