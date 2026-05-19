
namespace ITMartin.Media.Runtime.Interfaces;

public interface IWorkflowDefinition
{
    string Name { get; }

    IReadOnlyCollection<IWorkflowStep> Steps { get; }
}