// File: ITMartin.Media.Application/Abstractions/Workflows/IWorkflowExecutor.cs

namespace ITMartin.Media.Application.Abstractions.Workflows;

public interface IWorkflowExecutor
{
    Task ExecuteAsync(
        IWorkflowDefinition workflow,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default);
}