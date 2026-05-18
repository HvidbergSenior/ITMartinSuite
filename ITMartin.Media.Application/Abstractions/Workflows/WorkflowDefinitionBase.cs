// File: ITMartin.Media.Application/Workflows/WorkflowDefinitionBase.cs

namespace ITMartin.Media.Application.Abstractions.Workflows;

public abstract class WorkflowDefinitionBase
    : IWorkflowDefinition
{
    private readonly List<IWorkflowStep> _steps = [];

    public abstract string Name { get; }

    public IReadOnlyCollection<IWorkflowStep> Steps => _steps;

    protected void AddStep(IWorkflowStep step)
    {
        _steps.Add(step);
    }
}