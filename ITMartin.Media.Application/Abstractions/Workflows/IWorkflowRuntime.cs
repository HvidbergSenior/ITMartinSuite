// File: ITMartin.Media.Application/Abstractions/Workflows/IWorkflowRuntime.cs

namespace ITMartin.Media.Application.Abstractions.Workflows;

public interface IWorkflowRuntime
{
    Task StartAsync(
        WorkflowExecutionRequest request,
        CancellationToken cancellationToken = default);
}