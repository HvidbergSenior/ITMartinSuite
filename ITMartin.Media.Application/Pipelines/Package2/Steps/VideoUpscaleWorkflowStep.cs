using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoUpscaleWorkflowStep
    : IWorkflowStep
{
    private readonly IVideoEnhancementService
        _videoEnhancementService;

    public string Name =>
        nameof(VideoUpscaleWorkflowStep);

    public VideoUpscaleWorkflowStep(
        IVideoEnhancementService videoEnhancementService)
    {
        _videoEnhancementService =
            videoEnhancementService;
    }

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        if (context.State is not Package2WorkflowState state)
        {
            return;
        }

        if (!state.EnableUpscaling)
        {
            return;
        }

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Video &&
                         x.CurrentWorkingPath is not null))
        {
            var operation =
                new EnhancementOperation
                {
                    Name = Name,
                    StartedAt = DateTimeOffset.UtcNow
                };

            try
            {
                item.CurrentWorkingPath =
                    await _videoEnhancementService
                        .UpscaleAsync(
                            item.CurrentWorkingPath!,
                            cancellationToken);

                operation.Success = true;
            }
            catch (Exception ex)
            {
                item.Failed = true;

                item.FailureReason =
                    ex.Message;

                operation.Success = false;

                operation.Metadata =
                    ex.ToString();
            }

            operation.CompletedAt =
                DateTimeOffset.UtcNow;

            item.Operations.Add(
                operation);
        }
    }
}