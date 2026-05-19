
namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public interface IWorkflowDefinition
{
    string Name { get; }

    IReadOnlyCollection<IWorkflowStep> Steps { get; }
}