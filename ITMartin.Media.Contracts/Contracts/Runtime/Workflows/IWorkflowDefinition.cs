namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public interface IWorkflowDefinition
{
    string Name { get; }

    WorkflowType WorkflowType { get; }

    IReadOnlyCollection<IWorkflowStep> Steps { get; }
}