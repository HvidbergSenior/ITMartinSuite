using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoSharpenWorkflowStep
    : Package2WorkflowStepBase
{
    public override string Name =>
        nameof(VideoSharpenWorkflowStep);

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State is not Package2WorkflowState state)
        {
            return;
        }

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Video))
        {
            await ExecuteOperationAsync(
                item,
                Name,
                async () =>
                {
                    await Task.CompletedTask;
                });
        }
    }
}