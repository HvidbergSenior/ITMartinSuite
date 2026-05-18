// File: ITMartin.Media.Application/Abstractions/Workflows/IWorkflowDefinition.cs

namespace ITMartin.Media.Application.Abstractions.Workflows;

public interface IWorkflowDefinition
{
    string Name { get; }

    IReadOnlyCollection<IWorkflowStep> Steps { get; }
}