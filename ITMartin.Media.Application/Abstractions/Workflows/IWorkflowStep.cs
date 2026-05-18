// File: ITMartin.Media.Application/Abstractions/Workflows/IWorkflowStep.cs

namespace ITMartin.Media.Application.Abstractions.Workflows;

public interface IWorkflowStep
{
    string Name { get; }

    Task ExecuteAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default);
}