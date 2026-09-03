using ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

// Light touch compared to AnalogDigitize's VHS/Hi8 denoise strengths - this is for
// clean modern phone sensor grain (mainly visible in low-light shots), not
// analog tape noise, so a much gentler hqdn3d setting is enough.
public sealed class VideoNoiseReductionWorkflowStep : AnalogDigitizeWorkflowStepBase
{
    private readonly ILogger<VideoNoiseReductionWorkflowStep> _logger;
    public override string Name => nameof(VideoNoiseReductionWorkflowStep);

    public VideoNoiseReductionWorkflowStep(ILogger<VideoNoiseReductionWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableNoiseReduction)
        {
            _logger.LogInformation("Skipping video noise reduction");
            return;
        }

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteOperationAsync(item, Name, () =>
            {
                item.VideoFilters.Add("hqdn3d=2:2:4:4");
                return Task.CompletedTask;
            }, _logger);
        }
    }
}
