using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public sealed class ImageDenoiseWorkflowStep
    : IWorkflowStep
{
    public string Name =>
        nameof(ImageDenoiseWorkflowStep);

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
    }
}