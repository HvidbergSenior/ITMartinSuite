using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class ImageColorCorrectionWorkflowStep
    : IWorkflowStep
{
    public string Name =>
        nameof(ImageColorCorrectionWorkflowStep);

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
    }
}