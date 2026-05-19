using ITMartin.Media.Runtime.Interfaces;

namespace ITMartin.Media.Runtime.Workflows;

public sealed class WorkflowDefinition
    : IWorkflowDefinition
{
    public string Name { get; init; } = string.Empty;

    public IReadOnlyCollection<IWorkflowStep> Steps { get; init; }
        = [];
}