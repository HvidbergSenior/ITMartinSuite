using ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

// -14 LUFS matches Instagram/TikTok/YouTube's own normalization target, so
// the platform doesn't re-adjust (and potentially clip) it again on upload.
public sealed class LoudnessNormalizationWorkflowStep : AnalogDigitizeWorkflowStepBase
{
    private readonly ILogger<LoudnessNormalizationWorkflowStep> _logger;
    public override string Name => nameof(LoudnessNormalizationWorkflowStep);

    public LoudnessNormalizationWorkflowStep(ILogger<LoudnessNormalizationWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableLoudnessNormalization)
        {
            _logger.LogInformation("Skipping loudness normalization");
            return;
        }

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteOperationAsync(item, Name, () =>
            {
                item.AudioFilters.Add("loudnorm=I=-14:TP=-1.5:LRA=11");
                return Task.CompletedTask;
            }, _logger);
        }
    }
}
