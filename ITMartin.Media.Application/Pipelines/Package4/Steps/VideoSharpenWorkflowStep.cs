using ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

public sealed class VideoSharpenWorkflowStep : AnalogDigitizeWorkflowStepBase
{
    private readonly ILogger<VideoSharpenWorkflowStep> _logger;
    public override string Name => nameof(VideoSharpenWorkflowStep);

    public VideoSharpenWorkflowStep(ILogger<VideoSharpenWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableSharpen)
        {
            _logger.LogInformation("Skipping sharpen");
            return;
        }

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteOperationAsync(item, Name, () =>
            {
                item.VideoFilters.Add("unsharp=5:5:0.5:5:5:0.0");
                return Task.CompletedTask;
            }, _logger);
        }
    }
}
